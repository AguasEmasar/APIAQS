using dotenv.net;
using LOGIN;
using LOGIN.Database;
using LOGIN.Entities;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));

var builder = WebApplication.CreateBuilder(args);

// Configuración de Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
builder.WebHost.UseUrls("http://*:4000");

try
{
    var startup = new Startup(builder.Configuration);
    startup.ConfigureServices(builder.Services);

    var app = builder.Build();

    // Configuración de middlewares
    app.UseRouting();
    startup.Configure(app, app.Environment);

    // Health Check
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    exception = e.Value.Exception?.Message,
                    duration = e.Value.Duration.ToString()
                }),
                totalDuration = report.TotalDuration.ToString()
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.UseSerilogRequestLogging();

    await InitializeDatabaseAsync(app);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

async Task InitializeDatabaseAsync(IHost app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<ApplicationDbContext>();

    int retries = 5;
    while (retries > 0)
    {
        try
        {
            logger.LogInformation("Connecting to database...");
            await context.Database.MigrateAsync();

            var userManager = services.GetRequiredService<UserManager<UserEntity>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await ApplicationDbSeeder.InitializeAsync(
                userManager,
                roleManager,
                context,
                services.GetRequiredService<ILoggerFactory>());

            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogError(ex, "Database initialization failed. Retries left: {Retries}", retries);
            if (retries == 0) throw;
            await Task.Delay(5000);
        }
    }
}
