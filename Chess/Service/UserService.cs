using Chess.Db;
using Chess.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Chess.Service
{
    public class UserService : IUserService
    {
        private readonly ChessDbContext _context;
        private readonly IConfiguration _configuration;


        public UserService (ChessDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<User?> Register(string nickname, string password)
        {

            bool exists = await _context.Users.AnyAsync(u =>  u.Nickname == nickname);
            if (exists) throw new Exception("The nickname its already taken");
            string passwordHashed = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Nickname = nickname,
                Password = passwordHashed
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
            
        }

        public async Task<string?> Login(string nickname, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);
            if (user == null) return null;
            bool isValid = BCrypt.Net.BCrypt.Verify(password, user.Password);
            if (!isValid) return null;
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, user.Nickname),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<User?> GetById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>u.Id == id);
            if (user == null) throw new Exception("User not found");
            return user;
        }

        public async Task<User?> GetByNickname(string nickname)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);
            if (user == null) throw new Exception("User not found");
            return user;
        }

    }
}
