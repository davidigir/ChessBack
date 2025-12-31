using Chess.Dto;
using Chess.Service;
using Chess.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Chess.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ConfigSettings _configSettings;
        public UserController(IUserService userService, IOptions<ConfigSettings> options)
        {
            _userService = userService;
            _configSettings = options.Value;
        }
        [HttpPost("register")]
        public async Task<ActionResult<UserResponseDto>> Register([FromBody] UserRegisterDto dto)
        {
            try
            {
                var user = await _userService.Register(dto.Nickname, dto.Password);
                if (user == null) return BadRequest("User has not been created");
                var token = await _userService.Login(dto.Nickname, dto.Password);
                if (token == null) return Unauthorized(new { message = "Bad Credentials" });
                SetTokenCookie(token);

                var response = new UserResponseDto(user.Id, user.Nickname);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);

            }
            catch (Exception ex)
            {
                return Conflict(new { message = ex.Message });
            }

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto dto)
        {
            var token = await _userService.Login(dto.Nickname, dto.Password);
            if (token == null) return Unauthorized(new { message = "Bad Credentials" });
            SetTokenCookie(token);

            return Ok(new { message = "Login correctly" });
        }

        [HttpGet("id/{id}")]
        public async Task<ActionResult<UserResponseDto>> GetUser(int id)
        {
            try
            {
                var user = await _userService.GetById(id);
                if (user == null) return NotFound("User does not exist");
                var response = new UserResponseDto(user.Id, user.Nickname);

                return Ok(response);


            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", details = ex.Message });
            }
        }
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _userService.GetByNickname(
                User.Identity?.Name
                );
            return Ok(new
            {
                username = user.Nickname,
                elo = user.Elo,
                id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UserUpdateDto model)
        {
            Console.WriteLine(model.Nickname.ToString());
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);
            try
            {
                var updatedUser = await _userService.UpdateUser(userId, model);
                var newToken = _userService.GenerateToken(updatedUser);

                SetTokenCookie(newToken);
                return Ok(new
                {
                    updatedUser.Nickname
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            try
            {
                var user = await _userService.GetById(userId);
                return Ok(new
                {
                    user.Nickname,
                    user.Email
                });

            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("games")]
        public async Task<IActionResult> GetGames()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            try
            {
                var games = await _userService.GetGamesByUser(userId);
                return Ok(games);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        private void SetTokenCookie(string token)
        {
            Response.Cookies.Append(_configSettings.Auth.CookieName, token, new CookieOptions
            {
                HttpOnly = _configSettings.Auth.CookieHttpOnly,
                Secure = _configSettings.Auth.CookieSecure,
                SameSite = SameSiteMode.Lax,
                Path = _configSettings.Auth.CookiePath,
                Expires = DateTime.UtcNow.AddHours(_configSettings.Auth.CookieExpirationTime)
            });
        }
        //[Authorize]
        [HttpGet("review/{gameId}")]
        public async Task<IActionResult> GetGameById(Guid gameId)
        {
            try
            {
                var game = await _userService.GetGameById(gameId);
                Console.Write(game.ToString());
                if(game == null)
                {
                    return NotFound(new { message = "Game not found" });
                }
                return Ok(game);
            }
            catch (Exception ex)
            {
                return NotFound(new { message = "Game not found" });

            }
        }
        [Authorize]
        [HttpGet("stats")]
        public async Task<IActionResult> GetUserStats()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            int userId = int.Parse(userIdClaim);

            try
            {
                var stats = await _userService.GetStatsById(userId);
                return Ok(stats);

            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            Response.Cookies.Delete(_configSettings.Auth.CookieName, new CookieOptions
            {
                Path = _configSettings.Auth.CookiePath,
                Secure = _configSettings.Auth.CookieSecure,
                HttpOnly = _configSettings.Auth.CookieHttpOnly
            });
            return Ok(new { message = "Logged out" });
        }
    }
}
