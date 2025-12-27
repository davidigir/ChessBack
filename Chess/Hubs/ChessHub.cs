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
            var game = _gameService.GetGame(gameId);
            if (game == null) return;
            if (game.CurrentGameState != GameState.Playing) return;
            await Clients.OthersInGroup(gameId.ToString()).SendAsync("SendDrawRequest");
        }

        public async Task HandleRequestRematch (Guid gameId)
        {           
            //await Clients.Caller.SendAsync("CreateRematchRoom");
            await Clients.OthersInGroup(gameId.ToString()).SendAsync("SendRematchRequest");


        }

        public async Task HandleResignGame (Guid gameId)
        {
            var game = _gameService.GetGame(gameId);
            if (game == null) return;
            if (game.CurrentGameState != GameState.Playing) return;
            var nickname = Context.User?.Identity?.Name;
            if (nickname == null || nickname == "") return;
            await _gameService.TryResignGame(gameId, nickname);
            
            await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());

            await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
            {
                white = game.WhitePlayer?.Nickname,
                whiteIsReady = game.WhitePlayer?.IsReady,
                whitePlayerOnline = game.WhitePlayer?.IsConnected,
                blackPlayerOnline = game.BlackPlayer?.IsConnected,
                black = game.BlackPlayer?.Nickname,
                blackIsReady = game.BlackPlayer?.IsReady,
                status = game.CurrentGameState.ToString(),
                roomName = game.RoomName

            });

        }

        public async Task HandleAcceptDraw(Guid gameId)
        {
            var game = _gameService.GetGame(gameId);
            if (game == null) return;

            if (game.Finish == GameOverReason.PLAYING)
            {
                await _gameService.TryDrawGame(gameId);
                await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", GameOverReason.DRAW.ToString());

                await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
                {
                    white = game.WhitePlayer?.Nickname,
                    whiteIsReady = game.WhitePlayer?.IsReady,
                    whitePlayerOnline = game.WhitePlayer?.IsConnected,
                    blackPlayerOnline = game.BlackPlayer?.IsConnected,
                    black = game.BlackPlayer?.Nickname,
                    blackIsReady = game.BlackPlayer?.IsReady,
                    status = game.CurrentGameState.ToString(),
                    roomName = game.RoomName


                });

            }

        }

        public async Task HandleAcceptRematch(Guid gameId)
        {
            var request = new Dto.CreateGameRequestDto
            {
                RoomName = "Rematch",
                Password = "123"
            };
            
            Game game = _gameService.StartNewGame(request);

            await Clients.Group(gameId.ToString()).SendAsync("HandleJoinGameByRematch", game.Id);
        }

        public async Task SendPlayerReady(Guid gameId)
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
            await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
            {
                white = game.WhitePlayer?.Nickname,
                whiteIsReady = game.WhitePlayer?.IsReady,
                whitePlayerOnline = game.WhitePlayer?.IsConnected,
                blackPlayerOnline = game.BlackPlayer?.IsConnected,
                black = game.BlackPlayer?.Nickname,
                blackIsReady = game.BlackPlayer?.IsReady,
                status = game.CurrentGameState.ToString(),
                roomName = game.RoomName

            });


        }

        public async Task MakeMove(Guid gameId, string move)
        {
            string groupName = gameId.ToString();
            string senderId = Context.ConnectionId;

            Console.WriteLine($"[DEBUG] {move}");
            _gameService.TryMakeMove(gameId, move);
            GameOverReason finishType = _gameService.IsTheGameFinished(gameId);
            if (finishType != GameOverReason.PLAYING)
            {   
                await Clients.Group(groupName).SendAsync("GameOverReason", finishType.ToString());
                var game = _gameService.GetGame(gameId);

                await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
                {
                    white = game.WhitePlayer?.Nickname,
                    whiteIsReady = game.WhitePlayer?.IsReady,
                    whitePlayerOnline = game.WhitePlayer?.IsConnected,
                    blackPlayerOnline = game.BlackPlayer?.IsConnected,
                    black = game.BlackPlayer?.Nickname,
                    blackIsReady = game.BlackPlayer?.IsReady,
                    status = game.CurrentGameState.ToString(),
                    roomName = game.RoomName


                });

            }
            string fenBoard = _gameService.GetFenBoard(gameId);
            await Clients.Group(groupName).SendAsync("MoveReceived", senderId, move);
            await Clients.Group(groupName).SendAsync("PlayerTurn", _gameService.getCurrentTurn(gameId).ToString());
            await Clients.Group(groupName).SendAsync("BoardFen", fenBoard);
            await Clients.Groups(groupName).SendAsync("MovesHistory", _gameService.getStringMovesHistory(gameId));

        }

        public override async Task OnConnectedAsync()
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

                    await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
                    {
                        white = game.WhitePlayer?.Nickname,
                        whiteIsReady = game.WhitePlayer?.IsReady,
                        whitePlayerOnline = game.WhitePlayer?.IsConnected,
                        blackPlayerOnline = game.BlackPlayer?.IsConnected,
                        black = game.BlackPlayer?.Nickname,
                        blackIsReady = game.BlackPlayer?.IsReady,
                        status = game.CurrentGameState.ToString(),
                        roomName = game.RoomName


                    });

                    string fenBoard = _gameService.GetFenBoard(gameId);
                    await Clients.Group(gameId.ToString()).SendAsync("GameOverReason", game.Finish.ToString());
                    await Clients.Group(gameId.ToString()).SendAsync("BoardFen", fenBoard);


                }
            }
            Console.WriteLine("[OnCOnnected]");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
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


                

                await Clients.Group(gameId.ToString()).SendAsync("GameStatus", new
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

            }

            await base.OnDisconnectedAsync(exception);
        }

    }
}
