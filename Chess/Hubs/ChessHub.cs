using Chess.Dto;
using Chess.Enums;
using Chess.Model;
using Chess.Service;
using Chess.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Mscc.GenerativeAI.Types;
using System.Drawing;
using System.Net.NetworkInformation;

namespace Chess.Hubs
{
    [Authorize]
    public class ChessHub : Hub
    {
        private readonly GameService _gameService;
        private readonly ConfigSettings _configSettings;

        public ChessHub(GameService gameService, IOptions<ConfigSettings> options)
        {
            _gameService = gameService;
            _configSettings = options.Value;
        }

        //user joins a game
        public async Task JoinGame(Guid gameId)
        {
            Console.WriteLine($"Hub : {gameId}");

            string groupName = gameId.ToString();
            string connectionId = Context.ConnectionId;

            try
            {

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);


                Console.WriteLine($"Hub Conexion {Context.ConnectionId} in the group: {groupName}");


            }

            catch (Exception ex)
            {
                Console.WriteLine($"Hub Excepcion in JoinGame: {ex.Message}");
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
                await Clients.Caller.SendAsync("ErrorMessage", "Request draw error");
        Console.WriteLine($"Hub Rematch Error");
            }
}

        public async Task HandleRequestRematch (Guid gameId)
        {
            //TODO Refactor this request match propertly with old and new games
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
                Console.WriteLine($"Hub Rematch Error");
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
                await Clients.Caller.SendAsync("ErrorMessage", "Resign Game Error");
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
                    RoomName = _configSettings.Gameplay.RematchRoomName,
                    Password = _configSettings.Gameplay.RematchRoomPassword
                };
            

                Game game = _gameService.StartRematchGame(gameId, request);

                await Clients.Group(gameId.ToString()).SendAsync("HandleJoinGameByRematch", game.Id);
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Hub Rematch Error");
            }
        }
        public async Task GetValidMoves(Guid gameId, string pos)
        {
   
                if (string.IsNullOrWhiteSpace(pos) || pos.Length < 2)
                {
                    await Clients.Caller.SendAsync("RecieveValidMoves", new List<Coordinate>());
                    return;
                }

            try
            {
                var moves = await _gameService.GetValidMoves(gameId, pos);

                await Clients.Caller.SendAsync("RecieveValidMoves", moves ?? new List<Coordinate>());

                Console.WriteLine($"Hub pos: {pos}");

            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("RecieveValidMoves", new List<Coordinate>());
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");

                Console.WriteLine($"HUb error with the moves: {ex.Message}");

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
                    color,
                    nickname,
                    status
                });
                await NotifyGameStatus(gameId, game);

            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Hub SendPlayerReady Error");
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


                await NotifyAllGame(gameId, game, senderId, move);
               

            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Time to rematch out");
                Console.WriteLine($"Hub SetPromoteTo Error");
            }
        }

        public async Task MakeMove(Guid gameId, string move)
        {
            try
            {

            
                string groupName = gameId.ToString();
                string senderId = Context.ConnectionId;

                Console.WriteLine($"Hub {move}");

                await _gameService.TryMakeMove(gameId, move);

                GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
                var game = _gameService.GetGame(gameId);

                if (finishType != GameOverReason.PLAYING)
                {   
                    await Clients.Group(groupName).SendAsync("GameOverReason", finishType.ToString());


                }

                await NotifyAllGame(gameId, game, senderId, move);

                //BOT SECTION
                //lets ask to bot to move if there is any
                await _gameService.BotTryToMove(gameId, game);

            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Error Moving");
                Console.WriteLine($"Hub Make move Error");
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
                    bool isReconnection = false;
                    if (game.WhitePlayer?.Nickname == nickname)
                    {
                        assignedColor = "White";
                        isReconnection = !string.IsNullOrEmpty(game.WhitePlayer.ConnectionId);
                        game.WhitePlayer.ConnectionId = Context.ConnectionId;
                        statusPlayer = game.WhitePlayer.IsReady;
                        game.WhitePlayer.IsConnected = true;
                    }
                    else if (game.BlackPlayer?.Nickname == nickname)
                    {
                        assignedColor = "Black";
                        isReconnection = !string.IsNullOrEmpty(game.BlackPlayer.ConnectionId);

                        game.BlackPlayer.ConnectionId = Context.ConnectionId;
                        statusPlayer = game.BlackPlayer.IsReady;
                        game.BlackPlayer.IsConnected = true;
                    }
                    _gameService.StopTimeoutTimer(gameId, game);

                    if (assignedColor != "")
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());

                        await Clients.Caller.SendAsync("ReceiveMyStatus", new
                        {
                            color = assignedColor,
                            nickname = nickname,
                            status = statusPlayer
                        });
                        //Reconection Logic
                        if (isReconnection) await Clients.OthersInGroup(gameId.ToString()).SendAsync("PlayerReconnected", nickname);
                        if (!isReconnection) await Clients.OthersInGroup(gameId.ToString()).SendAsync("PlayerJoined", nickname);


                        await Clients.Group(gameId.ToString()).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());

                        await NotifyGameStatus(gameId, game);

                        string fenBoard = _gameService.GetFenBoard(gameId);
                        await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());
                        await Clients.Group(gameId.ToString()).SendAsync("BoardFen", fenBoard);
                           
                    }
                }
                Console.WriteLine("Hub Connected");
                await base.OnConnectedAsync();
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Connexion error");
                Console.WriteLine($"Hub connextion error");
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
                    await Clients.OthersInGroup(groupName).SendAsync("PlayerDisconected", nickname);




                await NotifyGameStatus(gameId, game);

               

            }

            await base.OnDisconnectedAsync(exception);
            }

            catch
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Error disconnection player");
                await base.OnDisconnectedAsync(exception);

                Console.WriteLine($"Hub Error disconnection player");
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
        private async Task NotifyAllGame(Guid gameId, Game game, string senderId, string move) {
            string fenBoard = _gameService.GetFenBoard(gameId);


            await NotifyGameStatus(gameId, game);
            await Clients.Group(gameId.ToString()).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(gameId.ToString()).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(gameId.ToString()).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(gameId.ToString()).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));

        }
}
