using Chess.Model;

namespace Chess.Service
{
    public interface IUserService
    {
        Task<User?> Register(string nickname, string password);
        Task<string?> Login(string nickname, string password);
        Task<User?> GetById(int id);
    }
}
