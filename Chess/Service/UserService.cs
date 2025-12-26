using Chess.Db;
using Chess.Dto;
using Chess.Entity;
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

        public async Task<UserEntity?> Register(string nickname, string password)
        {

            bool exists = await _context.Users.AnyAsync(u =>  u.Nickname == nickname);
            if (exists) throw new Exception("The nickname its already taken");
            string passwordHashed = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new UserEntity
            {
                Nickname = nickname,
                Password = passwordHashed,
                Email = ""
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

            return GenerateToken(user);
        }

        public string GenerateToken(UserEntity user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.Name, user.Nickname),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        }),
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<UserEntity?> GetById(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u =>u.Id == id);
            if (user == null) throw new Exception("User not found");
            return user;
        }

        public async Task<UserEntity?> GetByNickname(string nickname)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);
            if (user == null) throw new Exception("User not found");
            return user;
        }
        public async Task<UserEntity?> UpdateUser(int userId, UserUpdateDto model)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);

                if (user == null) throw new Exception("User not found");

                if (user.Nickname != model.Nickname)
                {
                    bool alreadyExists = await _context.Users.AnyAsync(u => u.Nickname == model.Nickname);
                    if (alreadyExists) throw new Exception("Nickname already in use");

                    user.Nickname = model.Nickname;
                }

                user.Email = model.Email;

                if (!string.IsNullOrEmpty(model.Password))
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
                }

                await _context.SaveChangesAsync();

                return user;
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating user: " + ex.Message);
            }
        }

        public async Task<Object> GetGamesByUser(int userId)
        {
            try
            {
                var games = await _context.Games
                    .Include(g => g.WhitePlayer)
                    .Include(g => g.BlackPlayer)
                    .Where(g => g.WhitePlayerId == userId || g.BlackPlayerId == userId)
                    .OrderByDescending(g => g.CreatedAt)
                    .Select(g => new {
                        g.Id,
                        g.CreatedAt,
                        g.Result,
                        g.PgnHistory,
                        // Proyectamos solo lo necesario de los jugadores
                        WhitePlayer = new { g.WhitePlayer.Id, g.WhitePlayer.Nickname },
                        BlackPlayer = new { g.BlackPlayer.Id, g.BlackPlayer.Nickname },
                        g.WhitePlayerId,
                        g.BlackPlayerId
                    })
                    .ToListAsync();

                return games;
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving games: " + ex.Message);
            }
        }

        public async Task<object?> GetGameById(Guid gameId)
        {
            try
            {
                var game = await _context.Games
                    .Include(g => g.WhitePlayer)
                    .Include(g => g.BlackPlayer)
                    .Where(g => g.Id == gameId)
                    .Select(g => new {
                        g.Id,
                        g.CreatedAt,
                        g.Result,
                        g.PgnHistory,
                        WhitePlayer = new { g.WhitePlayer.Id, g.WhitePlayer.Nickname },
                        BlackPlayer = new { g.BlackPlayer.Id, g.BlackPlayer.Nickname },
                        g.WhitePlayerId,
                        g.BlackPlayerId
                    })
                    .FirstOrDefaultAsync();

                return game;
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving game: " + ex.Message);
            }
        }

        public async Task<StatsDto> GetStatsById(int userId)
        {
            try
            {
                var stats = await _context.Games
                    .Where(g => g.WhitePlayerId == userId || g.BlackPlayerId == userId)
                    .GroupBy(g => 1)
                    .Select(group => new
                    {
                        TotalGames = group.Count(),
                        TotalWins = group.Count(g =>
                            (g.WhitePlayerId == userId && g.Result == "WHITE_WINS") ||
                            (g.BlackPlayerId == userId && g.Result == "BLACK_WINS")),
                        WhiteWins = group.Count(g => g.WhitePlayerId == userId && g.Result == "WHITE_WINS"),
                        BlackWins = group.Count(g => g.BlackPlayerId == userId && g.Result == "BLACK_WINS"),
                        WhiteGames = group.Count(g => g.WhitePlayerId == userId),
                        BlackGames = group.Count(g => g.BlackPlayerId == userId),
                        Draws = group.Count(g => g.Result == "DRAW"),
                        Losses = group.Count(g =>
                            (g.Result == "BLACK_WINS" && g.WhitePlayerId == userId) ||
                            (g.Result == "WHITE_WINS" && g.BlackPlayerId == userId))
                    })
                    .FirstOrDefaultAsync();

                if (stats == null || stats.TotalGames == 0)
                {
                    return new StatsDto { TotalGames = 0 };
                }

                return new StatsDto
                {
                    TotalGames = stats.TotalGames,
                    TotalWins = stats.TotalWins,
                    TotalDraws = stats.Draws,
                    TotalLosses = stats.Losses,
                    Winrate = Math.Round((double)stats.TotalWins / stats.TotalGames * 100, 2),

                    BlackWinrate = stats.BlackGames > 0
                        ? Math.Round((double)stats.BlackWins / stats.BlackGames * 100, 2)
                        : 0,

                    WhiteWinrate = stats.WhiteGames > 0
                        ? Math.Round((double)stats.WhiteWins / stats.WhiteGames * 100, 2)
                        : 0
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error calculating statistics");
            }
        }


    }
}
