using MPC.VCO.DST.Infrastructure;
using MPC.VCO.DST.Application;
using Microsoft.AspNetCore.HttpOverrides;
using MPC.VCO.DST.WebSite.API.Configuration;


namespace MPC.VCO.DST.WebSite.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddOptimizerKeyVaultConfiguration(builder.Configuration);

            builder.Services.AddControllers();

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto;

                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            builder.Services.AddCors(options =>
                options.AddPolicy(
                    "UiCorsPolicy",
                    policy =>
                    {
                        string[] origins =
                            builder.Configuration
                                .GetSection("Cors:AllowedOrigins")
                                .Get<string[]>()
                            ?? [];

                        if (origins.Length > 0)
                        {
                            policy
                                .WithOrigins(origins)
                                .AllowAnyHeader()
                                .AllowAnyMethod();
                        }
                    }));

            var envKey = Environment.GetEnvironmentVariable("CosmosDb__Key");

            //Console.WriteLine(
            //    $"Environment CosmosDb__Key exists: {!string.IsNullOrWhiteSpace(envKey)}");

            //var configKey = builder.Configuration["CosmosDb:Key"];

            //Console.WriteLine(
            //    $"Configuration CosmosDb:Key exists: {!string.IsNullOrWhiteSpace(configKey)}");

            builder.Services.AddInfrastructure(builder.Configuration); // Add infrastructure services DI
            builder.Services.AddApplication(); 
            builder.Services.AddAuthenticationServices(builder.Configuration);
            builder.Services.AddAzureAdOptions(builder.Configuration);
            builder.Services.AddOpenApiServices();
            builder.Services.AddApiExceptionHandling();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseOpenApiServices();

            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseExceptionHandler();

            app.UseCors("UiCorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }
    }
}  