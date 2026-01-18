using Chess.Db;
using Chess.Dto;
using Chess.Entity;
using Chess.Enums;
using Chess.Hubs;
using Chess.Model;
using Chess.Settings;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Mscc.GenerativeAI;
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
        private readonly ConfigSettings _configSettings;
        private readonly IAService _IAService;



        public GameService(IHubContext<ChessHub> hubContext, IServiceScopeFactory scopeFactory, IOptions<ConfigSettings> options, IAService iaService)
        {
            _hubContext = hubContext;
            _scopeFactory = scopeFactory;
            _configSettings = options.Value;
            _IAService = iaService;
        }


        public Dictionary<string, bool> getPlayersState(Guid gameId)
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
            if (!_activeGames.TryGetValue(gameId, out var game)) return "";
            return string.Join(",", game.MovesHistory);
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
                HandleBotJoin(gameId, newGame);
                return newGame;
            }
            else
            {
                throw new InvalidOperationException("Failed to start new game due to GUID collision.");
            }
        }

        public void HandleBotJoin(Guid gameId, Game game)
        {
            game.JoinBotTimer?.Dispose();

            if (game.BlackPlayer != null) return;

            game.JoinBotTimer = new System.Threading.Timer(async (state) =>
            {
                int elo = (game.WhitePlayer?.Elo ?? 100) + 30;
                Player bot = new Player("MiniCarlsen", 999, elo, PieceColor.Black);
                bot.IsReady = true;
                bot.IsConnected = true;
                bot.IsBot = true;
                game.BlackPlayer = bot;

                Console.WriteLine($"Hub Bot joined the game {gameId}");

                await _hubContext.Clients.Group(gameId.ToString())
                    .SendAsync("PlayerJoined", bot.Nickname);
                var status = new GameStatusDto

                {
                    White = game.WhitePlayer?.Nickname,
                    WhiteIsReady = game.WhitePlayer?.IsReady ?? false,
                    WhitePlayerOnline = game.WhitePlayer?.IsConnected ?? false,
                    WhitePlayerElo = game.WhitePlayer?.Elo ?? 0,
                    Black = game.BlackPlayer?.Nickname,
                    BlackPlayerElo = game.BlackPlayer?.Elo ?? 0,
                    BlackIsReady = game.BlackPlayer?.IsReady ?? false,
                    BlackPlayerOnline = game.BlackPlayer?.IsConnected ?? false,
                    Status = game.CurrentGameState.ToString(),
                    RoomName = game.RoomName
                };

                await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameStatus", status);

                

                game.JoinBotTimer?.Dispose();
                game.JoinBotTimer = null;

            }, gameId, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        }

        public async Task BotTryToMove(Guid gameId, Game game)
        {
            if (game?.BlackPlayer == null || !game.BlackPlayer.IsBot) return;

            var fen = GetFenBoard(gameId);
            string movesHistory = string.Join(",", game.MovesHistory);

            Console.WriteLine($"Bot IAFEN: {fen}");
            Console.WriteLine($"Bot IAMoves: {movesHistory}");
            bool success = false;
            int maxTry = 0;
            string move = "";
            while (!success && maxTry<10)
            {
                await Task.Delay(1000);
                move = await _IAService.GetMove(fen, movesHistory);
                Console.WriteLine($"Bot {maxTry} ");
                Console.WriteLine($"Bot {move}");

                success = await TryMakeMove(gameId, move);
                maxTry++;
            }
            if (success)
            {
                { /**
                await _hubContext.Clients.Group(gameId.ToString()).SendAsync("MoveReceived", game.BlackPlayer.Id, move);
                await _hubContext.Clients.Group(gameId.ToString()).SendAsync("PlayerTurn", getCurrentTurn(gameId).ToString());
                await _hubContext.Clients.Group(gameId.ToString()).SendAsync("BoardFen", fen);
                await _hubContext.Clients.Groups(gameId.ToString()).SendAsync("MovesHistory", getStringMovesHistory(gameId));
                    */
                }


                string groupName = gameId.ToString();

                await _hubContext.Clients.Group(groupName).SendAsync("MoveReceived", game.BlackPlayer.Id, move);

                await _hubContext.Clients.Group(groupName).SendAsync("BoardFen", GetFenBoard(gameId));

                await _hubContext.Clients.Group(groupName).SendAsync("PlayerTurn", getCurrentTurn(gameId).ToString());

                await _hubContext.Clients.Group(groupName).SendAsync("MovesHistory", getStringMovesHistory(gameId));

                GameOverReason finishType = IsTheGameFinished(gameId);
                if (finishType != GameOverReason.PLAYING)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("GameOverReason", finishType.ToString());
                }

                await NotifyGameStatus(gameId, game);
            }
            else
            {
                Console.WriteLine("IA: Error");
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
        public async Task<string> TryPromotePiece(Guid gameId, PieceType piece)
        {
            Game game = GetGame(gameId);
            if (game == null) return "";
            bool shouldFinalize = false;
            string movement = "";


            lock (_lock)
            {
                if (game.CurrentGameState != GameState.Promoting) return "";
                {
                    movement = game.PromotePiece(piece);
                    if (movement != "" && game.CurrentGameState == GameState.Finished)
                    {
                        shouldFinalize = true;
                    }
                }


            }
            if (shouldFinalize)
            {
                await HandleFinishGame(gameId, game);
            }

            return movement;
        }

        public async Task<bool> TryMakeMove(Guid gameId, string move)
        {
            Game game = GetGame(gameId);
            if (game == null) return false;

            // flag
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

        public async Task<List<Coordinate>> GetValidMoves(Guid gameId, string pos)
        {
            Game game = GetGame(gameId);
            if (game == null) return [];
            Coordinate source = Coordinate.FromAlgebraic(pos);
            List<Coordinate> coords = MovementValidator.GetValidMoves(game.Board, source, game.LastMove);
            return coords;
        }



        private async Task HandleFinishGame(Guid gameId, Game game)
        {
            try
            {
                double whiteScore = 0.5;
                if (game.Finish == GameOverReason.BLACK_WINS ||
                    game.Finish == GameOverReason.WHITE_SURRENDERS ||
                    game.Finish == GameOverReason.WHITE_DISCONNECTED)
                {
                    whiteScore = 0.0;
                }
                else if (game.Finish == GameOverReason.DRAW ||
                         game.Finish == GameOverReason.STALEMATE)
                {
                    whiteScore = 0.5;
                }
                else
                {
                    whiteScore = 1.0;
                }

                int kFactor = _configSettings.Gameplay.KFactor;
                var delta = EloCalculator.CalculateDelta(
                    game.WhitePlayer.Elo,
                    game.BlackPlayer.Elo,
                    whiteScore,
                    kFactor
                );

                game.EloChange = delta;
                game.WhiteEloBefore = game.WhitePlayer.Elo;
                game.BlackEloBefore = game.BlackPlayer.Elo;



                game.WhitePlayer.Elo = Math.Max(_configSettings.Gameplay.MinimumElo, game.WhitePlayer.Elo + delta);
                game.BlackPlayer.Elo = Math.Max(_configSettings.Gameplay.MinimumElo, game.BlackPlayer.Elo - delta);

                game.WhiteEloAfter = game.WhitePlayer.Elo;
                game.BlackEloAfter = game.BlackPlayer.Elo;

                await SaveGameToDatabase(gameId);
                await NotifyGameStatus(gameId, game);
                //_activeGames.TryRemove(gameId, out _);
                //TODO WE SHOULD HANDLE THIS OLD ACTIVE GAMES IN A DIFFERENT WAY bc we are removing active and old games at the same time
                _oldGames.TryAdd(gameId, game);

                game.CleanTimer?.Dispose();
                game.CleanTimer = new System.Threading.Timer((state) =>
                {
                    Guid gId = (Guid)state;
                    _activeGames.TryRemove(gameId, out _);
                    _oldGames.TryRemove(gId, out var g);
                    g?.CleanTimer?.Dispose();
                    Console.WriteLine($"Cleanup: Game {gId} removed from oldgames.");
                }, gameId, TimeSpan.FromSeconds(_configSettings.Gameplay.InactivityDeleteTimeoutSeconds), Timeout.InfiniteTimeSpan);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to finalize game {gameId}: {ex.Message}");
            }
        }

        private async Task NotifyGameStatus(Guid gameId, Game game)
        {

            var status = new GameStatusDto

            {
                White = game.WhitePlayer?.Nickname,
                WhiteIsReady = game.WhitePlayer?.IsReady ?? false,
                WhitePlayerOnline = game.WhitePlayer?.IsConnected ?? false,
                WhitePlayerElo = game.WhitePlayer?.Elo ?? 0,
                Black = game.BlackPlayer?.Nickname,
                BlackPlayerElo = game.BlackPlayer?.Elo ?? 0,
                BlackIsReady = game.BlackPlayer?.IsReady ?? false,
                BlackPlayerOnline = game.BlackPlayer?.IsConnected ?? false,
                Status = game.CurrentGameState.ToString(),
                RoomName = game.RoomName
            };
            
            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameStatus", status);
            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());

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
                game.CleanTimer = null;
                game.JoinBotTimer?.Dispose();
                game.JoinBotTimer = null;
                
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
                    game.JoinBotTimer?.Dispose();
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
            lock (_lock)
            {

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
                else if (game.CurrentGameState == GameState.Playing)

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

                    }, null, TimeSpan.FromSeconds(_configSettings.Gameplay.InactivityDeleteTimeoutSeconds), Timeout.InfiniteTimeSpan
                    );
                }       


            }


        }

        private void StartTimeoutTimer(Guid gameId)
        {
            double seconds = _configSettings.Gameplay.ReconnectionTimeoutSeconds;
            System.Timers.Timer timer = new System.Timers.Timer(seconds * 1000);
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

        private async Task HandleTimeout(Guid gameId)
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

        public void StopTimeoutTimer(Guid gameId, Game game)
        {
            //if (game.CurrentGameState == GameState.Finished)
            //TODO We should handle if the game was already finished
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
                    BlackEloBefore = game.BlackEloBefore,
                    BlackEloAfter = game.BlackEloAfter,
                    WhiteEloBefore = game.WhiteEloBefore,
                    WhiteEloAfter = game.WhiteEloAfter,
                    EloChange = game.EloChange,
                    TotalMovements = game.MovesHistory.Count,
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

    }
}