using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Wistellar.Core.Services;

namespace Wistellar.Server.Services
{
    public class LocalAuthenticationService(
        IOptions<JwtBearerOptions> jwtOptions,
        IUserService userService) : ILocalAuthenticationService
    {
        public async Task<string> SignInAsync(string username, string password)
        {
            var options = jwtOptions.Value;

            var isAuthenticated = await userService.CheckCredentialsAsync(username, password);
            if (isAuthenticated)
            {
                var creds = new SigningCredentials(
                    options.TokenValidationParameters.IssuerSigningKey,
                    SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: options.TokenValidationParameters.ValidIssuer,
                    audience: options.Audience,
                    claims: [new Claim(ClaimTypes.Name, username)],
                    expires: DateTime.Now.AddMonths(1),
                    signingCredentials: creds);

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            throw new UnauthorizedAccessException();
        }
    }
}
