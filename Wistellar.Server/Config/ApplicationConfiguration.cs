namespace Wistellar.Server.Config
{
    public static class ApplicationConfiguration
    {
        public static void ConfigureApplication(this WebApplication app)
        {
            // Serves the built SvelteKit SPA out of wwwroot.
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRateLimiter();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();
            app.UseAuthorization();

            app.MapControllers();
        }
    }
}