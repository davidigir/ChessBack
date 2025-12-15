using Chess.Enums;
using Chess.Model;
using System.Collections.Concurrent;

namespace Chess.Service
{
    public class GameService
    {

        private readonly ConcurrentDictionary<Guid, Game> _activeGames = new ConcurrentDictionary<Guid, Game>();
        private readonly Dictionary<string, Guid> _connectionToGameMap = new Dictionary<string, Guid>();
        private readonly object _lock = new object();


        public string AssignColorToConnection(Guid gameId, string connectionId)
        {
            lock (_lock)
            {
                if(!_activeGames.TryGetValue(gameId, out var game))
                {
                    throw new KeyNotFoundException($"Game With ID {gameId} not found");
                }
                if (game.WhitePlayerConnectionId == connectionId) return "w";
                if (game.BlackPlayerConnectionId == connectionId) return "b";
                if (!game.IsWhiteAssigned)
                {
                    game.WhitePlayerConnectionId = connectionId;
                    return "w";
                }
                if (!game.IsBlackAssigned)
                {
                    game.BlackPlayerConnectionId = connectionId;
                    return "b";
                }
                throw new InvalidOperationException("Room full");

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

        public bool CanJoinGame(Guid gameId)
        {
            if (_activeGames.TryGetValue(gameId, out var game))
            {
                return !game.IsFull();
            }
            // Si la partida no existe, no puede unirse.
            return false;
        }

        public void AddConnectionToGame(Guid gameId, string connectionId)
        {
            lock (_lock)
            {

            if (_activeGames.TryGetValue(gameId, out var game))
            {
                if (!game.ConnectionIds.Contains(connectionId) && game.ConnectionIds.Count < 2)
                {
                        _connectionToGameMap[connectionId] = gameId;
                        game.ConnectionIds.Add(connectionId);
                }
                else if (game.ConnectionIds.Count >= 2)
                {
                    throw new InvalidOperationException("Game is full (2 players max)");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Game {gameId} not found.");
            }
            }

        }

        public void RemoveConnectionFromGame(Guid gameId, string connectionId)
        {
            lock (_lock)
            {
                if (!_activeGames.TryGetValue(gameId, out var game)) 
                {
                    throw new KeyNotFoundException($"Game {gameId} not found");
                }

                if (game.ConnectionIds.Contains(connectionId))
                {
                    game.ConnectionIds.Remove(connectionId);
                    _connectionToGameMap.Remove(connectionId);


                    if (game.BlackPlayerConnectionId == connectionId)
                    {
                        game.BlackPlayerConnectionId = null;
                    }

                    if (game.WhitePlayerConnectionId == connectionId)
                    {
                        game.WhitePlayerConnectionId = null;
                    }


                    string remainingConnections = string.Join(", ", _connectionToGameMap.Keys);
                    Console.WriteLine($"[DEBUG-MAP] Conexion {connectionId} deleted. Remaining: ({_connectionToGameMap.Count}): {remainingConnections}");
                }
            }
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
    }
}