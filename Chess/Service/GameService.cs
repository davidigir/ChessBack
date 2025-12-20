using Chess.Enums;
using Chess.Hubs;
using Chess.Model;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Chess.Service
{
    public class GameService
    {

        private readonly ConcurrentDictionary<Guid, Game> _activeGames = new ConcurrentDictionary<Guid, Game>();
        private readonly Dictionary<string, Guid> _connectionToGameMap = new Dictionary<string, Guid>();
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<Guid, System.Timers.Timer> _reconnectionTimers = new();

        private readonly IHubContext<ChessHub> _hubContext;
        

        public GameService(IHubContext<ChessHub> hubContext)
        {
            _hubContext = hubContext;
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

        public Game StartNewGame()
        {
            Game newGame = new Game();
            Guid gameId = Guid.NewGuid();
            newGame.Id = gameId;

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

        public Game GetGame(Guid gameId)
        {
            _activeGames.TryGetValue(gameId, out Game game);
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

        public bool TryMakeMove(Guid gameId, string move)
        {
            Game game = GetGame(gameId);

            if (game == null)
            {
                Console.WriteLine($"Error: Game {gameId} not found.");
                return false;
            }

            return game.MakeMove(move);
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

        public Guid? GetGameIdByConnectionId(string connectionId)
        {
            if (_connectionToGameMap.TryGetValue(connectionId, out var gameId))
            {
                return gameId;
            }
            return null;
        }



        public bool JoinGame(Guid gameId, string nickname)
        {
            Game game = this.GetGame(gameId);
            if (game == null) return false;
            lock (_lock)
            {
                //if (game.WhitePlayer != null && game.BlackPlayer != null) return false;
                if(game.WhitePlayer != null && game.WhitePlayer.Nickname == nickname && !game.WhitePlayer.IsConnected)
                {
                    //StopTimeoutTimer(gameId);

                    return true;
                }
                if (game.BlackPlayer != null && game.BlackPlayer.Nickname == nickname && !game.BlackPlayer.IsConnected)
                {
                    //StopTimeoutTimer(gameId);
                    return true;
                }
                if (game.WhitePlayer == null)
                {
                    game.WhitePlayer = new Player(nickname, PieceColor.White);
                    Console.WriteLine($"Jugador {nickname}");
                    return true;

                }
                else if (game.BlackPlayer == null)
                {
                    game.BlackPlayer = new Player(nickname, PieceColor.Black);
                    Console.WriteLine($"Jugador {nickname}");

                    return true;
                }
                Console.WriteLine("Te vas fuera");
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
                    Console.WriteLine($"Error en timeout: {ex.Message}");
                }
            };

            _reconnectionTimers[gameId] = timer;
            timer.Start();
            Console.WriteLine("Timer iniciado");


        }

        private async void HandleTimeout(Guid gameId)
        {
            var game = GetGame(gameId);
            if (game == null) return;

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

            });

            await _hubContext.Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());
        }

        public void StopTimeoutTimer(Guid gameId)
        {
            Console.WriteLine("Intentando parar");
            if (_reconnectionTimers.TryRemove(gameId, out System.Timers.Timer timer))
            {
                timer.Stop();
                timer.Dispose();
                Console.WriteLine("Player reconected");
            }
        }
    } }