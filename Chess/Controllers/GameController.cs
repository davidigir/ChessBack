using Chess.Dto;
using Chess.Model;
using Chess.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Chess.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class GameController : ControllerBase
    {

        private readonly GameService _gameService;
        private readonly IUserService _userService;

        public GameController(GameService gameService, IUserService userService)
        {
            _gameService = gameService;
            _userService = userService;

        }

        [HttpPost("start")]
        public async Task<ActionResult<Guid>> StartGame()
        {
            Game newGame = _gameService.StartNewGame();

            return Ok(newGame.Id);
        }

        [HttpGet("display/{gameId}")]
        public async Task<ActionResult<String>> DisplayGame(Guid gameId)
        {
            Game game = _gameService.GetGame(gameId);
            if (game == null) return NotFound($"Partida {gameId} no encontrada");

            game.Board.DisplayBoard();
            return Ok($"Mostrando {gameId}");
        }


        [HttpGet("export/{gameId}")]
        public async Task<ActionResult<String>> GetFenBoard(Guid gameId)
        {

            Game game = _gameService.GetGame(gameId);
            if (game == null) return NotFound($"Partida {gameId} no encontrada");
            game.Board.GetFenPlacement();

            return Ok($"FEN del game {gameId} mostrado");



        }
        [HttpPost("move/{gameId}")]
        public async Task<ActionResult<String>> MakeMove(Guid gameId, [FromBody] MoveRequestDto request)
        {
            if (request == null) return BadRequest("Datos de movimiento requeridos.");

            bool success = _gameService.TryMakeMove(gameId, request.Move);
            if (success)
            {
                return Ok("Movimento ejecutado");

            }
            else
            {
                return BadRequest("Movimiento Ilegal");
            }
        }
        [HttpGet("games")]
        public async Task<ActionResult<List<Guid>>> GamesList()
        {
            List<Guid> gameIds = _gameService.GetActiveGameIds();

            if (gameIds.Count == 0)
            {
                return NoContent();
            }

            return Ok(gameIds);
        }

        [Authorize]
        [HttpPost("join/{gameId}")]
        public async Task<ActionResult<bool>> JoinGame(Guid gameId)
        {
            Console.WriteLine("Testtt");

            var nickname = User.Identity?.Name;
            if (string.IsNullOrEmpty(nickname)) return Unauthorized();
            Console.WriteLine("Test");


            User? user = await _userService.GetByNickname(nickname);
            if (user == null) return NotFound("User not found");

            bool joined = _gameService.JoinGame(gameId, nickname);

            if (joined)
            {
                return Ok(new { message = "Joined successfully", gameId = gameId });
            }
            else
            {
                return BadRequest("Could not join: Game is full");
            }


        }
    }
}
