using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using Wistellar.Core;
using Wistellar.Core.Import;
using Wistellar.Core.Services;
using Wistellar.Core.Services.MobileNetwork;
using Wistellar.Core.Services.Vendor;
using Wistellar.Server.Authentication;
using Wistellar.Server.Import;
using Wistellar.Server.Services;

namespace Wistellar.Server.Config
{
    public static class ServiceConfiguration
    {
        public const string OnlyOneConcurrencyPolicy = "StrongConcurrency";
        public static void ConfigureServices(this WebApplicationBuilder builder)
        {
            IServiceCollection services = builder.Services;

            // Rate limiting configuration
            services.AddRateLimiter(options =>
            {
                options.AddConcurrencyLimiter(policyName: OnlyOneConcurrencyPolicy, options =>
                {
                    options.PermitLimit = 1;
                    options.QueueLimit = 0;
                });

                options.GlobalLimiter =
                      PartitionedRateLimiter.CreateChained(
                            PartitionedRateLimiter.Create<HttpContext, string>(
                                httpContext =>
                                RateLimitPartition.GetSlidingWindowLimiter(
                                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                                    factory: partition => new SlidingWindowRateLimiterOptions
                                    {
                                        SegmentsPerWindow = 10,
                                        AutoReplenishment = true,
                                        PermitLimit = 600,
                                        QueueLimit = 10,
                                        Window = TimeSpan.FromSeconds(60)
                                    }
                                )
                            )
                      );
            });

            // Authentication configuration
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer();

            // Host and form options configuration
            services.Configure<HostOptions>(options =>
            {
                options.ServicesStartConcurrently = true;
                options.ServicesStopConcurrently = true;
            })
            .Configure<AppSettings>(builder.Configuration.GetSection("Wistellar"))
            .Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = long.MaxValue;
            });

            services.ConfigureOptions<ConfigureJwtBearerOptions>();
            services.AddScoped<ILocalAuthenticationService, LocalAuthenticationService>();

            // Database context configuration
            services.AddTransient<WiGleBackupContext>((v) =>
            {
                var settings = v.GetRequiredService<IOptions<AppSettings>>();
                var context = new WiGleBackupContext(settings.Value.ConnectionString);
                context.Database.Migrate();
                return context;
            });

            // Response compression configuration
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Fastest);
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                options.EnableForHttps = true;
                options.MimeTypes =
                [
                    "application/vnd.mapbox-vector-tile"
                ];
            });

            // Hosted services. The resolvers are registered twice on purpose: once as the singleton
            // used for lookups, and once as an IHostedService resolving that same instance, so their
            // caches are warmed before the app starts taking requests.
            services.AddSingleton<OuiFetchService>();
            services.AddSingleton<OuiDbService>();
            services.AddTransient<VendorResolverService>();
            services.AddTransient<IVendorResolverService>(p => p.GetRequiredService<VendorResolverService>()!);
            services.AddSingleton<IHostedService>(p => p.GetRequiredService<VendorResolverService>());

            services.AddSingleton<MccMncFetchService>();
            services.AddSingleton<MccMncDbService>();
            services.AddSingleton<MobileNetworkResolverService>();
            services.AddTransient<IMobileNetworkResolverService>(p => p.GetRequiredService<MobileNetworkResolverService>()!);
            services.AddSingleton<IHostedService>(p => p.GetRequiredService<MobileNetworkResolverService>());

            // Application services
            services.AddScoped<DatabaseService>();
            services.AddScoped<GeoJsonLocationsService>();
            services.AddScoped<GeoJsonNetworksService>();
            services.AddScoped<IUserService, UserService>();

            // Data importers
            services.AddSingleton<ITextImport, CsvOpenCellIdImport>();
            services.AddSingleton<ITextImport, CsvMylnikovCellImport>();
            services.AddSingleton<ITextImport, CsvWigleObservationsImport>();
            services.AddScoped<StreamImporter>();

            // CORS configuration
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    policy =>
                    {
                        policy.WithOrigins(
                            "http://localhost:5173",
                            "http://localhost:5174"
                            ).AllowAnyHeader().WithMethods("GET").AllowCredentials();
                    });
            });

            // Controllers and JSON configuration
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new NetTopologySuite.IO.Converters.GeoJsonConverterFactory());
            });

            services.AddEndpointsApiExplorer();

            // Swagger configuration
            services.ConfigureSwagger();
        }
    }
}