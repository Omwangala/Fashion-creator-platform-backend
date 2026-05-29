namespace backend.Services
{
    public interface ITokenService
    {
        string CreateToken(int userId, string username); 
    }
}