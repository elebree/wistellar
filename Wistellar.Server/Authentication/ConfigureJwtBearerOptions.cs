using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Text;
using Wistellar.Server.Config;

namespace Wistellar.Server.Authentication
{
    public class ConfigureJwtBearerOptions(
        IOptions<AppSettings> settings,
        IWebHostEnvironment env
    ) : IConfigureNamedOptions<JwtBearerOptions>
    {
        private readonly AppSettings settings = settings.Value;

        public void Configure(JwtBearerOptions options)
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = static context =>
                {
                    var authorization = context.Request.Headers.Authorization;
                    if (AuthenticationHeaderValue.TryParse(authorization, out var header))
                    {
                        if (header.Scheme == "Basic" && header.Parameter != null)
                        {
                            // The WiGLE app replays its stored credential as Basic auth with the
                            // token in the password field, so drop the username and keep the rest.
                            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
                            string usernamePassword;
                            try
                            {
                                usernamePassword = encoding.GetString(Convert.FromBase64String(header.Parameter));
                            }
                            catch (FormatException)
                            {
                                // Malformed credentials are simply not authenticated.
                                return Task.CompletedTask;
                            }

                            context.Token = string.Join(":", usernamePassword.Split(":").Skip(1));
                            return Task.CompletedTask;
                        }
                        else if (header.Scheme == "Bearer")
                        {
                            context.Token = header.Parameter;
                            return Task.CompletedTask;
                        }
                    }

                    context.NoResult();
                    return Task.CompletedTask;
                }
            };

            var keyFilePath = IssuerSigningKeyManager.GetKeyFilePath(env.ContentRootPath, settings.IssuerSigningKeyFilePath);
            var key = IssuerSigningKeyManager.GetSymmetricSecurityKey(keyFilePath);

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidIssuer = "wistellar",
                ClockSkew = TimeSpan.Zero,
            };

            options.Events.OnTokenValidated = (ctx) =>
            {
                if (ctx?.Principal?.Identity?.Name != null)
                    ctx.Response.Headers["X-Username"] = ctx.Principal.Identity.Name;
                return Task.CompletedTask;
            };

            options.Audience = "wistellar";
            options.ClaimsIssuer = "wistellar";
        }

        public void Configure(string? name, JwtBearerOptions options)
        {
            Configure(options);
        }
    }
}
