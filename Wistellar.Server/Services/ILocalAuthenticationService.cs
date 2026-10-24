namespace Wistellar.Server.Services
{
    public interface ILocalAuthenticationService
    {
        /// <returns>A signed JWT, or <c>null</c> when the credentials are rejected.</returns>
        Task<string?> SignInAsync(string username, string password);
    }
}