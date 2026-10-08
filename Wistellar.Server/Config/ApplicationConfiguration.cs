using Microsoft.Extensions.Options;

namespace Wistellar.Server.Config
{
    public static class ApplicationConfiguration
    {
        public static void ConfigureApplication(this WebApplication app)
        {
            // Has to run before anything that reads the client address - the rate limiter
            // partitions on it. Only registered when explicitly enabled; see ForwardedHeadersSettings.
            var forwarded = app.Services.GetRequiredService<IOptions<AppSettings>>().Value.ForwardedHeaders;
            if (forwarded.Enabled)
            {
                if (forwarded.KnownProxies.Length == 0 && forwarded.KnownNetworks.Length == 0)
                {
                    app.Logger.LogWarning(
                        "Forwarded headers are enabled with no known proxies or networks, so X-Forwarded-For " +
                        "is accepted from any peer. Anything able to reach the app directly can then spoof its " +
                        "own address and evade rate limiting. Set Wistellar:ForwardedHeaders:KnownNetworks.");
                }

                app.UseForwardedHeaders();
            }

            // Serves the built SvelteKit SPA out of wwwroot.
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRateLimiter();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    // Absolute endpoint so the UI never depends on page-relative resolution of the
                    // definition URL (the default "v1/swagger.json" is resolved against the browser
                    // URL in index.js, which is brittle). Leading "/" is left as-is by that script.
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Wistellar API v1");
                });
            }

            app.UseCors();
            app.UseAuthorization();

            app.MapControllers();

            // Anonymous and exempt from rate limiting, so a container health probe cannot be
            // starved by ordinary traffic and cannot consume the caller's own budget.
            app.MapHealthChecks("/health").DisableRateLimiting();
        }
    }
}
