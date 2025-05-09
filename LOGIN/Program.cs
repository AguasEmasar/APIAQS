using dotenv.net;
using LOGIN;
using LOGIN.Database;
using LOGIN.Entities;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
builder.WebHost.UseUrls("http://*:4000"); // Configuración única del puerto

try
{
    var startup = new Startup(builder.Configuration);
    startup.ConfigureServices(builder.Services);

    var app = builder.Build();

    // Configuración de middlewares en el orden correcto
    app.UseRouting();
    startup.Configure(app, app.Environment);

    // Health Check con configuración detallada
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

    // Swagger solo en desarrollo
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Cargar variables de entorno
    DotEnv.Load(options: new DotEnvOptions(probeForEnv: true));

    // Middleware para logging de requests
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    // Inicialización de la base de datos con reintentos
    await InitializeDatabaseAsync(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación se detuvo inesperadamente");
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
            logger.LogInformation("Intentando conectar a la base de datos...");
            
            if (!await context.Database.CanConnectAsync())
            {
                throw new Exception("No se pudo conectar a la base de datos");
            }
            
            logger.LogInformation("Conexión a la base de datos exitosa");
            
            await context.Database.MigrateAsync();
            
            var userManager = services.GetRequiredService<UserManager<UserEntity>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await ApplicationDbSeeder.InitializeAsync(userManager, roleManager, context, logger);
            
            break;
        }
        catch (Exception ex)
        {
            retries--;
            logger.LogError(ex, "Error al conectar con la base de datos. Reintentos restantes: {Retries}", retries);
            
            if (retries == 0)
            {
                logger.LogCritical("No se pudo conectar a la base de datos después de varios intentos");
                throw;
            }
                
            await Task.Delay(5000);
        }
    }
}
