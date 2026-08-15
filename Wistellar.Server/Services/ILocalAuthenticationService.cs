namespace Wistellar.Server.Services
{
    public interface ILocalAuthenticationService
    {
        Task<string> SignInAsync(string username, string password);
    }
}