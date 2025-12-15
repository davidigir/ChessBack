using Chess.Model;
using Chess.Service;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Hubs
{
    public class ChessHub : Hub
    {
        private readonly GameService _gameService;

        public ChessHub(GameService gameService)
        {
            _gameService = gameService;
        }

        //user joins a game
        public async Task JoinGame(Guid gameId)
        {
            Console.WriteLine($"[DEBUG-HUB] : {gameId}");

            string groupName = gameId.ToString();
            string connectionId = Context.ConnectionId;
            string assignedColor = "";

            try
            {

                if (!_gameService.CanJoinGame(gameId))
                {
                    await Clients.Caller.SendAsync("JoinFailed", "The game is full");
                    return;
                       
                }
                _gameService.AddConnectionToGame(gameId, connectionId);
                assignedColor = _gameService.AssignColorToConnection(gameId, connectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);


                Console.WriteLine($"[DEBUG-HUB] Conexion {Context.ConnectionId} in the group: {groupName}");



                await Clients.Caller.SendAsync("GameJoined", gameId, assignedColor);
                await Clients.OthersInGroup(groupName).SendAsync("PlayerJoined", connectionId);
                await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
                string fenBoard = _gameService.GetFenBoard(gameId);
                await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
                
                await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId)); //if the game has not started should be "" but maybe an spectator




                Console.WriteLine($"[DEBUG-HUB] Confirmation 'GameJoined' sended to {Context.ConnectionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR-HUB] Excepcion in JoinGame: {ex.Message}");
                await Clients.Caller.SendAsync("JoinFailed", ex.Message);
            }
        }

        public async Task MakeMove(Guid gameId, string move)
        {
            string groupName = gameId.ToString();
            string senderId = Context.ConnectionId;

            Console.WriteLine($"[DEBUG] {move}");
            _gameService.TryMakeMove(gameId, move);
            string fenBoard = _gameService.GetFenBoard(gameId);
            await Clients.Group(groupName).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));

        }

        public async Task StartGame(Guid gameId)
        {
            string groupName = gameId.ToString();

        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;
            Guid? gameId = _gameService.GetGameIdByConnectionId(connectionId);
            if (gameId.HasValue)
            {
                string groupName = gameId.ToString();
                _gameService.RemoveConnectionFromGame(gameId.Value, connectionId);
                await Clients.Group(groupName).SendAsync("PlayerDisconected", "Player off");
                Console.WriteLine($"DEBUG, {connectionId} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
            
          

        }

    }
}
