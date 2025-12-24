using Chess.Dto;
using Chess.Entity;

namespace Chess.Service
{
    public interface IUserService
    {
        Task<UserEntity?> Register(string nickname, string password);
        Task<string?> Login(string nickname, string password);
        Task<UserEntity?> GetById(int id);
        Task<UserEntity?> GetByNickname(string nickname);
        Task<UserEntity?> UpdateUser(int userId, UserUpdateDto model);
        string GenerateToken(UserEntity user);
        Task<Object> GetGamesByUser(int userId);
        Task<object?> GetGameById(Guid gameId);






    }
}
