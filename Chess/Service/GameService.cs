using Chess.Db;
using Chess.Dto;
using Chess.Entity;
using Chess.Enums;
using Chess.Hubs;
using Chess.Model;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using static Azure.Core.HttpHeader;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Chess.Service
{
    public class GameService
    {

        private readonly ConcurrentDictionary<Guid, Game> _activeGames = new ConcurrentDictionary<Guid, Game>();
        private readonly ConcurrentDictionary<Guid, Game> _oldGames = new ConcurrentDictionary<Guid, Game>();

        //private readonly Dictionary<string, Guid> _connectionToGameMap = new Dictionary<string, Guid>();
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<Guid, System.Timers.Timer> _reconnectionTimers = new();

        private readonly IHubContext<ChessHub> _hubContext;
        private readonly IServiceScopeFactory _scopeFactory;

        

        public GameService(IHubContext<ChessHub> hubContext, IServiceScopeFactory scopeFactory)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
        }


        public Dictionary<string, bool> getPlayersState(Guid gameId)
        {
            lock (_lock)
            {
                if (!_activeGames.TryGetValue(gameId, out var game))
                {
                    throw new KeyNotFoundException($"Game With ID {gameId} not found");
                }

                Dictionary<string, bool> playerStates = new Dictionary<string, bool>();
                playerStates.Add("b", game.BlackPlayer.IsReady);
                playerStates.Add("w", game.WhitePlayer.IsReady);
                return playerStates;


            }
        }



        public PieceColor getCurrentTurn(Guid gameId)
        {
            if (!_activeGames.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException($"Game With ID {gameId} not found");
            }
            return game.CurrentTurn;
        }

        public string getStringMovesHistory(Guid gameId)
        {
            if (!_activeGames.TryGetValue(gameId, out var game))
            {
                throw new KeyNotFoundException($"Game With ID {gameId} not found");
            }
            string stringMoves = "";
            List<string> movesHistory = game.MovesHistory;
            foreach (string move in movesHistory)
            {
                stringMoves += move + ",";
            }
            return stringMoves;
        }

        public Game StartNewGame(CreateGameRequestDto request)
        {
            Game newGame = new Game();
            Guid gameId = Guid.NewGuid();
            newGame.Id = gameId;
            if (!string.IsNullOrEmpty(request.Password)) newGame.SetPassword(request.Password);

            if (!string.IsNullOrEmpty(request.RoomName)) newGame.RoomName = request.RoomName;


            if (_activeGames.TryAdd(gameId, newGame))
            {
                Console.WriteLine($"New Game started with the UUID: {gameId}");
                return newGame;
            }
            else
            {
                throw new InvalidOperationException("Failed to start new game due to GUID collision.");
            }
        }

        public Game StartRematchGame(Guid oldGameId, CreateGameRequestDto request)
        {
            var oldGame = GetOldGame(oldGameId);
            if (oldGame == null) throw new Exception("Old game not found");

            var newGame = new Game();
            Guid newId = Guid.NewGuid();
            newGame.Id = newId;

            newGame.RoomName = !string.IsNullOrEmpty(request.RoomName) ? request.RoomName : oldGame.RoomName;
            if (!string.IsNullOrEmpty(request.Password)) newGame.SetPassword(request.Password);

            lock (_lock)
            {
                if (oldGame.BlackPlayer != null)
                {
                    newGame.WhitePlayer = new Player(
                        oldGame.BlackPlayer.Nickname,
                        oldGame.BlackPlayer.Id,
                        oldGame.BlackPlayer.Elo,
                        PieceColor.White
                    );
                }

                if (oldGame.WhitePlayer != null)
                {
                    newGame.BlackPlayer = new Player(
                        oldGame.WhitePlayer.Nickname,
                        oldGame.WhitePlayer.Id,
                        oldGame.WhitePlayer.Elo,
                        PieceColor.Black
                    );
                }
            }

            if (_activeGames.TryAdd(newId, newGame))
            {
                Console.WriteLine($"Rematch started: {oldGameId} -> {newId}");
                return newGame;
            }

            throw new InvalidOperationException("Failed to start rematch.");
        }

        public Game GetGame(Guid gameId)
        {
            _activeGames.TryGetValue(gameId, out Game game);
            return game;
        }
        public Game GetOldGame(Guid gameId)
        {
            _oldGames.TryGetValue(gameId, out Game game);
            return game;
        }

        public string GetGamesList()
        {
            if (_activeGames.IsEmpty) return "There is no active Games";
            var gamesUuids = _activeGames.Keys;

            string gamesList = string.Join(",", gamesUuids.Select(g => g.ToString()));
            return "Active Games: " + gamesList;


        }

        public List<Guid> GetActiveGameIds()
        {
            if (_activeGames.IsEmpty)
            {
                return new List<Guid>();
            }

            return _activeGames.Keys.ToList();
        }

        public async Task<bool> TryMakeMove(Guid gameId, string move)
        {
            Game game = GetGame(gameId);
            if (game == null) return false;

            // Esta es nuestra "bandera"
            bool shouldFinalize = false;
            bool moveSuccess = false;

            lock (_lock)
            {
                if (game.CurrentGameState == GameState.Finished) return false;

                moveSuccess = game.MakeMove(move);

                if (moveSuccess && game.CurrentGameState == GameState.Finished)
                {
                    shouldFinalize = true;
                }
            } 

            if (shouldFinalize)
            {
                await HandleFinishGame(gameId, game);
            }

            return moveSuccess;
        }

        private async Task HandleFinishGame(Guid gameId, Game game)
        {
            try
            {
                double whiteScore = 0.5;
                if (game.Finish == GameOverReason.WHITE_WINS) whiteScore = 1.0;
                else if (game.Finish == GameOverReason.BLACK_WINS) whiteScore = 0.0;

                var (newWhite, newBlack) = EloCalculator.GetNewRatings(
                    game.WhitePlayer.Elo,
                    game.BlackPlayer.Elo,
                    whiteScore
                );

                game.WhitePlayer.Elo = newWhite;
                game.BlackPlayer.Elo = newBlack;

                await SaveGameToDatabase(gameId);

                _oldGames.TryAdd(gameId, game);

                //add timer and dont delete it instant
                // _activeGames.TryRemove(gameId, out _); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to finalize game {gameId}: {ex.Message}");
            }
        }
        public async Task TryDrawGame(Guid gameId)
        {
            Game game = GetGame(gameId);

            if (game == null) return;
            bool handle = false;

            lock (_lock)
            {
                if (game.CurrentGameState == GameState.Finished)
                {
                    return;
                }

                game.CurrentGameState = GameState.Finished;
                game.Finish = GameOverReason.DRAW;


                handle = true;
            }
            if (handle) await HandleFinishGame(gameId, game);
            
        }

        public async Task TryResignGame(Guid gameId, string nickname)
        {
            Game game = GetGame(gameId);

            if (game == null) return;
            bool handler = false;

            lock (_lock)
            {
                if (game.CurrentGameState == GameState.Finished)
                {
                    return;
                }

                game.CurrentGameState = GameState.Finished;
                if (game.WhitePlayer?.Nickname == nickname)
                {
                    game.Finish = GameOverReason.WHITE_SURRENDERS;


                }
                else if (game.BlackPlayer?.Nickname == nickname)
                {

                    game.Finish = GameOverReason.BLACK_SURRENDERS;

                }

                handler = true;


            }
            if (handler) await HandleFinishGame(gameId, game);

        }



        public GameOverReason IsTheGameFinished(Guid gameId)
        {
            Game game = GetGame(gameId);

            if (game == null)
            {
                Console.WriteLine($"Error: Game {gameId} not found.");
                throw new InvalidOperationException("No Game with this Guid");

            }
            return game.Finish;

        }





        public string GetFenBoard(Guid gameId)
        {
            Game game = this.GetGame(gameId);
            if (game != null) return game.Board.GetFenPlacement();
            else return "";
        }

        /*** QUIZA DEBERIA NECESITAR ESTO MAS ADELANTE
        public Guid? GetGameIdByConnectionId(string connectionId)
        {
            if (_connectionToGameMap.TryGetValue(connectionId, out var gameId))
            {
                return gameId;
            }
            return null;
        }

        **/
        public void DeleteGame(Guid gameId)
        {
            if (_activeGames.TryRemove(gameId, out var game))
            {
                game.CleanTimer?.Dispose();
                Console.WriteLine($"Game {gameId} Deleted by inactivity.");
            }
        }



        public bool JoinGame(Guid gameId, string nickname, int playerId, int playerElo, JoinRequestDto request)
        {
            Game game = this.GetGame(gameId);
            if (game == null) return false;

            if (game.IsPrivate && !game.CheckPassword(request.Password)) return false;

            lock (_lock)
            {
                bool joined = false;

                if ((game.WhitePlayer?.Id == playerId) || (game.BlackPlayer?.Id == playerId))
                {
                    Console.WriteLine($"Player {nickname} Re conected");
                    joined = true;
                }
                else if (game.WhitePlayer == null)
                {
                    
                    game.WhitePlayer = new Player(nickname, playerId, playerElo, PieceColor.White);
                    Console.WriteLine($"Player {nickname} Playing with white pieces");
                    joined = true;
                }
                else if (game.BlackPlayer == null)
                {
                    game.BlackPlayer = new Player(nickname, playerId, playerElo, PieceColor.Black);
                    Console.WriteLine($"Player {nickname} playing with black pieces");
                    joined = true;
                }

                if (joined)
                {
                    game.CleanTimer?.Dispose();
                    game.CleanTimer = null;
                    return true;
                }

                Console.WriteLine("Game Full");
                return false;
            }
        }


        public void HandlePlayerDisconnection(Guid gameId, string nickname)
        {
            lock (_lock){

                var game = this.GetGame(gameId);
                if (game == null)
                {

                }
                if (game.CurrentGameState == GameState.Waiting)
                {
                    if (game.WhitePlayer != null && game.WhitePlayer.Nickname == nickname)
                    {
                        Console.WriteLine("WhitePlayer");
                        game.WhitePlayer = null;

                    }
                    else if (game.BlackPlayer != null && game.BlackPlayer.Nickname == nickname)
                    {
                        Console.WriteLine("BlackPlayer");

                        game.BlackPlayer = null;
                    }
                }
                else if(game.CurrentGameState == GameState.Playing)

                {

                    Console.WriteLine("TimeOut");

                    var player = (game.WhitePlayer?.Nickname == nickname) ? game.WhitePlayer : game.BlackPlayer;
                    if (player != null) player.IsConnected = false;
                    StartTimeoutTimer(gameId);

                }

                if (game.WhitePlayer == null && game.BlackPlayer == null)
                {
                    game.CleanTimer = new System.Threading.Timer((state) =>
                    {
                        DeleteGame(gameId);

                    }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan
                    );
                }


            }


        }

        private void StartTimeoutTimer(Guid gameId)
        {
            System.Timers.Timer timer = new System.Timers.Timer(5000);
            timer.AutoReset = false;
            timer.Enabled = true;

            timer.Elapsed += (sender, e) =>
            {
                try
                {
                    HandleTimeout(gameId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in the timeout: {ex.Message}");
                }
            };

            _reconnectionTimers[gameId] = timer;
            timer.Start();
            Console.WriteLine("Timer Init");


        }

        private async void HandleTimeout(Guid gameId)
        {
            var game = GetGame(gameId);
            if (game == null) return;
            bool handler = false;

            lock (_lock)
            {
                if (game.WhitePlayer != null && !game.WhitePlayer.IsConnected)
                {
                    game.Finish = GameOverReason.WHITE_DISCONNECTED;

                }
                else if (game.BlackPlayer != null && !game.BlackPlayer.IsConnected)
                {
                    game.Finish = GameOverReason.BLACK_DISCONNECTED;
                }

                game.CurrentGameState = GameState.Finished;


                _reconnectionTimers.TryRemove(gameId, out _);

                Console.WriteLine(game.CurrentGameState.ToString());
                Console.WriteLine(game.Finish.ToString());
                handler = true;

            }
            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
            {
                white = game.WhitePlayer?.Nickname,
                whiteIsReady = game.WhitePlayer?.IsReady,
                black = game.BlackPlayer?.Nickname,
                blackIsReady = game.BlackPlayer?.IsReady,
                status = game.CurrentGameState.ToString(),
                whitePlayerOnline = game.WhitePlayer?.IsConnected,
                blackPlayerOnline = game.BlackPlayer?.IsConnected,
                roomName = game.RoomName


            });

            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());
            //we need to save the game in the db

            if (handler) await HandleFinishGame(gameId, game);


        }

        public void StopTimeoutTimer(Guid gameId)
        {
            if (_reconnectionTimers.TryRemove(gameId, out System.Timers.Timer timer))
            {
                timer.Stop();
                timer.Dispose();
                Console.WriteLine("Player reconected");
            }
        }

        public async Task SaveGameToDatabase(Guid gameId)
        {
            var game = GetGame(gameId);
            if (game == null) return;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ChessDbContext>();
                var gameRecord = new GameEntity
                {
                    Id = game.Id,
                    WhitePlayerId = game.WhitePlayer.Id,
                    BlackPlayerId = game.BlackPlayer.Id,
                    //TODO AÑADIR ELO
                    PgnHistory = string.Join(",", game.MovesHistory),
                    Result = game.Finish.ToString(),
                    CreatedAt = DateTime.UtcNow,
                };
                var whiteUser = await context.Users.FindAsync(game.WhitePlayer.Id);
                whiteUser.Elo = game.WhitePlayer.Elo;
                var blackUser = await context.Users.FindAsync(game.BlackPlayer.Id);
                blackUser.Elo = game.BlackPlayer.Elo;


                context.Games.Add(gameRecord);

                await context.SaveChangesAsync();
            }
        }

        public List<GameSummaryDto> GetActiveGamesSummary()
        {
            return _activeGames.Select(g => new GameSummaryDto
            {
                GameId = g.Key,
                RoomName = g.Value.RoomName ?? "",
                IsPrivate = g.Value.IsPrivate,
                Status = g.Value.CurrentGameState.ToString(),
                PlayerCount = (g.Value.WhitePlayer != null ? 1 : 0) +
                              (g.Value.BlackPlayer != null ? 1 : 0),
                WhitePlayer = g.Value.WhitePlayer?.Nickname ?? "Waiting...",
                BlackPlayer = g.Value.BlackPlayer?.Nickname ?? "Waiting..."

            }).ToList();
        }

    } }