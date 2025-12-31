using Chess.Db;
using Chess.Dto;
using Chess.Entity;
using Chess.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Chess.Service
{
    public class UserService : IUserService
    {
        private readonly ChessDbContext _context;
        private readonly JwtSettings _jwtSettings;


        public UserService (ChessDbContext context, IOptions<JwtSettings> options)
        {
            _context = context;
            _jwtSettings = options.Value; 
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
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.Name, user.Nickname),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        }),
                Expires = DateTime.UtcNow.AddHours(_jwtSettings.ExpirationTime),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
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
                        WhitePlayer = new { g.WhitePlayer.Id, g.WhitePlayer.Nickname },
                        BlackPlayer = new { g.BlackPlayer.Id, g.BlackPlayer.Nickname },
                        g.WhitePlayerId,
                        g.BlackPlayerId,
                        UserEloBefore = g.WhitePlayerId == userId ? g.WhiteEloBefore : g.BlackEloBefore,
                        UserEloAfter = g.WhitePlayerId == userId ? g.WhiteEloAfter : g.BlackEloAfter,
                        //elo is based on the whiteplayer point of view
                        UserEloChange = g.WhitePlayerId == userId ? g.EloChange : (g.EloChange * -1)
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
                var user = await GetById(userId);

                var whiteWinReasons = new[] { "WHITE_WINS", "BLACK_SURRENDERS", "BLACK_DISCONNECTED" };
                var blackWinReasons = new[] { "BLACK_WINS", "WHITE_SURRENDERS", "WHITE_DISCONNECTED" };
                var drawReasons = new[] { "DRAW", "STALEMATE" };

                var stats = await _context.Games
                    .Where(g => g.WhitePlayerId == userId || g.BlackPlayerId == userId)
                    .GroupBy(g => 1)
                    .Select(group => new
                    {
                        TotalGames = group.Count(),

                        TotalWins = group.Count(g =>
                            (g.WhitePlayerId == userId && whiteWinReasons.Contains(g.Result)) ||
                            (g.BlackPlayerId == userId && blackWinReasons.Contains(g.Result))),
                        WhiteDraws = group.Count(g => g.WhitePlayerId == userId && drawReasons.Contains(g.Result)),
                        BlackDraws = group.Count(g => g.BlackPlayerId == userId && drawReasons.Contains(g.Result)),
                        WhiteLosses = group.Count(g => g.WhitePlayerId == userId && blackWinReasons.Contains(g.Result)),
                        BlackLosses = group.Count(g => g.BlackPlayerId == userId && whiteWinReasons.Contains(g.Result)),
                        WhiteWins = group.Count(g => g.WhitePlayerId == userId && whiteWinReasons.Contains(g.Result)),
                        BlackWins = group.Count(g => g.BlackPlayerId == userId && blackWinReasons.Contains(g.Result)),
                        WhiteGames = group.Count(g => g.WhitePlayerId == userId),
                        BlackGames = group.Count(g => g.BlackPlayerId == userId),

                        Draws = group.Count(g => drawReasons.Contains(g.Result)),

                        Losses = group.Count(g =>
                            (g.WhitePlayerId == userId && blackWinReasons.Contains(g.Result)) ||
                            (g.BlackPlayerId == userId && whiteWinReasons.Contains(g.Result)))
                    })
                    .FirstOrDefaultAsync();

                if (stats == null || stats.TotalGames == 0)
                {
                    return new StatsDto { TotalGames = 0, Elo = user.Elo };
                }

                return new StatsDto
                {
                    TotalGames = stats.TotalGames,
                    TotalWins = stats.TotalWins,
                    TotalDraws = stats.Draws,
                    TotalLosses = stats.Losses,
                    Elo = user.Elo,
                    
                    BlackDraws = stats.BlackDraws,
                    BlackLosses = stats.BlackLosses,
                    WhiteDraws = stats.WhiteDraws,
                    WhiteLosses = stats.WhiteLosses,
                    
                    BlackGames = stats.BlackGames,
                    BlackWins = stats.BlackWins,
                    WhiteGames = stats.WhiteGames,
                    WhiteWins = stats.WhiteWins,

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
                throw new Exception($"Error calculating statistics: {ex.Message}");
            }
        }


    }
}
