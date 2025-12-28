using Chess.Dto;
using Chess.Entity;
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
        public async Task<ActionResult<Guid>> StartGame([FromBody] CreateGameRequestDto request)
        {
            Game newGame = _gameService.StartNewGame(request);

            return Ok(newGame.Id);
        }

        [HttpGet("display/{gameId}")]
        public async Task<ActionResult<String>> DisplayGame(Guid gameId)
        {
            Game game = _gameService.GetGame(gameId);
            if (game == null) return NotFound($"Game {gameId} not found");

            game.Board.DisplayBoard();
            return Ok($"Showing {gameId}");
        }


        [HttpGet("export/{gameId}")]
        public async Task<ActionResult<String>> GetFenBoard(Guid gameId)
        {

            Game game = _gameService.GetGame(gameId);
            if (game == null) return NotFound($"Game {gameId} not found");
            game.Board.GetFenPlacement();

            return Ok($"FEN {gameId}");



        }
        [HttpPost("move/{gameId}")]
        public async Task<ActionResult<String>> MakeMove(Guid gameId, [FromBody] MoveRequestDto request)
        {
            if (request == null) return BadRequest("Bad reqyest");

            bool success = await _gameService.TryMakeMove(gameId, request.Move);
            if (success)
            {
                return Ok("Sending Move");

            }
            else
            {
                return BadRequest("Ilegal Move");
            }
        }
        [HttpGet("games")]
        public ActionResult<List<GameSummaryDto>> GamesList()
        {
            var summaries = _gameService.GetActiveGamesSummary();

            if (!summaries.Any())
            {
                return NoContent(); 
            }

            return Ok(summaries);
        }

        [Authorize]
        [HttpPost("join/{gameId}")]
        public async Task<ActionResult<bool>> JoinGame(Guid gameId, [FromBody] JoinRequestDto request)
        {

            var nickname = User.Identity?.Name;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int playerId = int.Parse(userIdClaim);
            if (string.IsNullOrEmpty(nickname)) return Unauthorized();


            UserEntity? user = await _userService.GetByNickname(nickname);
            if (user == null) return NotFound("User not found");    



            bool joined = _gameService.JoinGame(gameId, nickname, playerId, user.Elo, request);

            if (joined)
            {
                return Ok(new { message = "Joined successfully", gameId = gameId });
            }
            else
            {
                return Unauthorized("Game full or wrong credentials");
            }


        }
        
    }
}
