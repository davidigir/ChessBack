using Chess.Enums;
using Chess.Model;
using Chess.Service;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Hubs
{
    public class ChessHubb : Hub
    {
        private readonly GameService _gameService;

        public ChessHubb(GameService gameService)
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

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);


                Console.WriteLine($"[DEBUG-HUB] Conexion {Context.ConnectionId} in the group: {groupName}");



                await Clients.Caller.SendAsync("GameJoined", gameId, assignedColor);
                await Clients.OthersInGroup(groupName).SendAsync("PlayerJoined", connectionId);
                await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
                string fenBoard = _gameService.GetFenBoard(gameId);
                await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
                Dictionary<string, bool> playerStates = _gameService.getPlayersState(gameId);
                await Clients.Group(groupName).SendAsync("PlayerReady", playerStates);

                await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId)); //if the game has not started should be "" but maybe an spectator
                GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
                if (finishType != GameOverReason.PLAYING) await Clients.Group(groupName).SendAsync("GameFinish", finishType.ToString());




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
            GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
            if ( finishType != GameOverReason.PLAYING) await Clients.Group(groupName).SendAsync("GameFinish", finishType.ToString());
            string fenBoard = _gameService.GetFenBoard(gameId);
            await Clients.Group(groupName).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));

        }

        public async Task PlayerReady(Guid gameId, string nickname, bool ready)
        {
            string groupName = gameId.ToString();
            string senderId = Context.ConnectionId;
            Console.WriteLine($"[PLAYER] {gameId} {nickname}  {ready}");
            //_gameService.ChangePlayerStatus(string nickname, bool ready);
            await Clients.Group(groupName).SendAsync("PlayerReady", "ready");
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
                await Clients.Group(groupName).SendAsync("PlayerDisconected", "Player off");
                Console.WriteLine($"DEBUG, {connectionId} disconnected");
                await Clients.Group(groupName).SendAsync("RoomFull", "e");

            }

            await base.OnDisconnectedAsync(exception);
            
          

        }

    }
}
