namespace backend.Services
{
    public interface ITokenService
    {
        string CreateToken(string username);
    }
}