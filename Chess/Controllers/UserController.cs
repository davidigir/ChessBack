using Chess.Dto;
using Chess.Service;
using Microsoft.AspNetCore.Mvc;

namespace Chess.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        public UserController(IUserService userService)
        {
            _userService = userService;
        }
        [HttpPost("register")]
        public async Task<ActionResult<UserResponseDto>> Register([FromBody] UserRegisterDto dto)
        {
            try
            {
                var user = await _userService.Register(dto.Nickname, dto.Password);
                if (user == null) return BadRequest("User has not been created");
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
            if (token == null) return Unauthorized(new {message = "Bad Credentials"});
            Response.Cookies.Append("X-Access-Token", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // in production must be true for https
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTime.UtcNow.AddHours(1)
            });

            return Ok(new {message = "Login correctly"});
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
    }
}
