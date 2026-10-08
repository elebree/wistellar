using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;

namespace Wistellar.Server.Config
{
    public static class SwaggerConfiguration
    {
        public static void ConfigureSwagger(this IServiceCollection services)
        {
            services.AddSwaggerGen(setup =>
            {
                setup.SwaggerDoc("v1", new OpenApiInfo { Title = "Wistellar API", Version = "v1" });
                var scheme = JwtBearerDefaults.AuthenticationScheme;
                var jwtSecurityScheme = new OpenApiSecurityScheme
                {
                    BearerFormat = "JWT",
                    Name = "JWT Authentication",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = scheme,
                    Description = "Paste the JWT returned by /api/v2/activate",
                };

                setup.AddSecurityDefinition(scheme, jwtSecurityScheme);

                setup.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    { new OpenApiSecuritySchemeReference(scheme, document), new List<string>() }
                });
            });
        }
    }
}