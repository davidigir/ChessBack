using Chess.Dto;
using Chess.Enums;
using Chess.Model;
using Chess.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Drawing;
using System.Net.NetworkInformation;

namespace Chess.Hubs
{
    [Authorize]
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

            try
            {

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);


                Console.WriteLine($"[DEBUG-HUB] Conexion {Context.ConnectionId} in the group: {groupName}");


            }

            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR-HUB] Excepcion in JoinGame: {ex.Message}");
                await Clients.Caller.SendAsync("JoinFailed", ex.Message);
            }
        }

        public async Task HandleRequestDraw (Guid gameId)
        {
            try { 
             var game = _gameService.GetGame(gameId);
            if (game == null) return;
            if (game.CurrentGameState != GameState.Playing) return;
            await Clients.OthersInGroup(gameId.ToString()).SendAsync("SendDrawRequest");

        }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
        Console.WriteLine($"Rematch Error");
            }
}

        public async Task HandleRequestRematch (Guid gameId)
        {
            try { 
            //await Clients.Caller.SendAsync("CreateRematchRoom");
            var game = _gameService.GetOldGame(gameId);
            if (game == null) return;
            var nickname = Context.User?.Identity?.Name;
            var rivalId = (game.WhitePlayer?.Nickname == nickname)
        ? game.BlackPlayer?.ConnectionId
        : game.WhitePlayer?.ConnectionId;
            if (!string.IsNullOrEmpty(rivalId)) { 
                await Clients.Client(rivalId).SendAsync("SendRematchRequest");
            }else{
            await Clients.OthersInGroup(gameId.ToString()).SendAsync("SendRematchRequest");

    }
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }


        }

        public async Task HandleResignGame (Guid gameId)
        {
            try { 
            var game = _gameService.GetGame(gameId);
            if (game == null) return;
            if (game.CurrentGameState != GameState.Playing) return;
            var nickname = Context.User?.Identity?.Name;
            if (nickname == null || nickname == "") return;
            await _gameService.TryResignGame(gameId, nickname);
            
            await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());

            await NotifyGameStatus(gameId, game);

        }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
        Console.WriteLine($"Rematch Error");
            }

}

        public async Task HandleAcceptDraw(Guid gameId)
        {
            try
            {

            var game = _gameService.GetGame(gameId);
            if (game == null) return;

            if (game.Finish == GameOverReason.PLAYING)
            {
                await _gameService.TryDrawGame(gameId);
                await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", GameOverReason.DRAW.ToString());

                await NotifyGameStatus(gameId, game);

            }
        }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
        Console.WriteLine($"Rematch Error");
            }

}

        public async Task HandleAcceptRematch(Guid gameId)
        {
            try
            {

            var request = new Dto.CreateGameRequestDto
            {
                RoomName = "Rematch",
                Password = "123"
            };
            

            Game game = _gameService.StartRematchGame(gameId, request);

            await Clients.Group(gameId.ToString()).SendAsync("HandleJoinGameByRematch", game.Id);
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }
        public async Task GetValidMoves(Guid gameId, string pos)
        {
            try { 
            if (string.IsNullOrWhiteSpace(pos) || pos.Length < 2)
            {
                await Clients.Caller.SendAsync("RecieveValidMoves", new List<Coordinate>());
                return;
            }

            try
            {
                Console.WriteLine($"Posición recibida: {pos}");
                var moves = await _gameService.GetValidMoves(gameId, pos);

                await Clients.Caller.SendAsync("RecieveValidMoves", moves ?? new List<Coordinate>());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando movimientos: {ex.Message}");
                await Clients.Caller.SendAsync("RecieveValidMoves", new List<Coordinate>());
            }
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }
        public async Task SendPlayerReady(Guid gameId)
        {
            try
            {

            
            var nickname = Context.User?.Identity?.Name;
            var game = _gameService.GetGame(gameId);
            var color = "";
            var status = false;

            if (game == null || nickname == null) return;

            if (game.WhitePlayer?.Nickname == nickname)
            {
                game.WhitePlayer.IsReady = !game.WhitePlayer.IsReady;
                color = "White";
                status = game.WhitePlayer.IsReady;
                  
            }
            else if (game.BlackPlayer?.Nickname == nickname)
            {
                game.BlackPlayer.IsReady = !game.BlackPlayer.IsReady;
                color = "Black";
                status = game.BlackPlayer.IsReady;

            }

            bool bothReady = (game.WhitePlayer?.IsReady ?? false) && (game.BlackPlayer?.IsReady ?? false);
            if (bothReady) game.CurrentGameState = GameState.Playing;
            await Clients.Caller.SendAsync("ReceiveMyStatus", new
            {
                color = color,
                nickname = nickname,
                status = status
            });
            await NotifyGameStatus(gameId, game);

            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }

        public async Task SetPromoteTo(Guid gameId, PieceType pieceTypeToPromote)
        {
            try
            {

            
            string groupName = gameId.ToString();
            string senderId = Context.ConnectionId;
            string move = await _gameService.TryPromotePiece(gameId, pieceTypeToPromote);

            GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
            var game = _gameService.GetGame(gameId);

            if (finishType != GameOverReason.PLAYING)
            {
                await Clients.Group(groupName).SendAsync("GameOverReason", finishType.ToString());


            }
            await NotifyGameStatus(gameId, game);

            string fenBoard = _gameService.GetFenBoard(gameId);
            await Clients.Group(groupName).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));

            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }

        public async Task MakeMove(Guid gameId, string move)
        {
            try
            {

            
            string groupName = gameId.ToString();
            string senderId = Context.ConnectionId;

            Console.WriteLine($"[DEBUG] {move}");
            await _gameService.TryMakeMove(gameId, move);
            GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
            var game = _gameService.GetGame(gameId);

            if (finishType != GameOverReason.PLAYING)
            {   
                await Clients.Group(groupName).SendAsync("GameOverReason", finishType.ToString());


            }
            await NotifyGameStatus(gameId, game);
            string fenBoard = _gameService.GetFenBoard(gameId);
            await Clients.Group(groupName).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }

        public override async Task OnConnectedAsync()
        {
            try
            {

            
            //jwt
            var nickname = Context.User?.Identity?.Name;
            var gameIdString = Context.GetHttpContext().Request.Query["gameId"];

            if (Guid.TryParse(gameIdString, out Guid gameId) && nickname != null)
            {
                var game = _gameService.GetGame(gameId);
                if (game == null) return;

                string assignedColor = "";
                bool statusPlayer = false;
                if (game.WhitePlayer?.Nickname == nickname)
                {
                    assignedColor = "White";
                    game.WhitePlayer.ConnectionId = Context.ConnectionId;
                    statusPlayer = game.WhitePlayer.IsReady;
                    game.WhitePlayer.IsConnected = true;
                }
                else if (game.BlackPlayer?.Nickname == nickname)
                {
                    assignedColor = "Black";
                    game.BlackPlayer.ConnectionId = Context.ConnectionId;
                    statusPlayer = game.BlackPlayer.IsReady;
                    game.BlackPlayer.IsConnected = true;
                }
                _gameService.StopTimeoutTimer(gameId);

                if (assignedColor != "")
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());

                    await Clients.Caller.SendAsync("ReceiveMyStatus", new
                    {
                        color = assignedColor,
                        nickname = nickname,
                        status = statusPlayer
                    });
                    await Clients.Group(gameId.ToString()).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());

                    await NotifyGameStatus(gameId, game);

                    string fenBoard = _gameService.GetFenBoard(gameId);
                    await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());
                    await Clients.Group(gameId.ToString()).SendAsync("BoardFen", fenBoard);


                }
            }
            Console.WriteLine("[OnCOnnected]");
            await base.OnConnectedAsync();
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Rematch Error");
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {

            
            var nickname = Context.User?.Identity?.Name;
            var gameIdString = Context.GetHttpContext().Request.Query["gameId"];

            if (Guid.TryParse(gameIdString, out Guid gameId) && nickname != null)
            {
                var game = _gameService.GetGame(gameId);
                if (game == null) return;

                string groupName = gameId.ToString();
                
                    //Only discconect the player
                    _gameService.HandlePlayerDisconnection(gameId, nickname);

                //Timeout 



                await NotifyGameStatus(gameId, game);

               

            }

            await base.OnDisconnectedAsync(exception);
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                await base.OnDisconnectedAsync(exception);

                Console.WriteLine($"Rematch Error");
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

            await Clients.Group(gameId.ToString()).SendAsync("GameStatus", status);
        }
    }
}
