using LOGIN.Services.Interfaces;
using LOGIN.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using LOGIN.Services;
using LOGIN.Dtos;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LOGIN
{
    public class Startup
    {
        private readonly string _corsPolicy = "CorsPolicy";

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var jwtSettings = Configuration.GetSection("JWT");
            var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT secret key is missing from configuration.");

            var connString = Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string is missing");

            // Configuración de DbContext con reintentos
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(connString, ServerVersion.AutoDetect(connString), 
                    mySqlOptions => {
                        mySqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore);
                        mySqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }));

            // Health Checks
           services.AddHealthChecks()
               .AddMySql(Configuration.GetConnectionString("DefaultConnection"))
               .AddDbContextCheck<ApplicationDbContext>();

            // Registro de servicios
            services.AddTransient<IUserService, UserService>();
            services.AddTransient<IEmailService, EmailService>();
            services.AddTransient<IComunicateServices, ComunicateServices>();
            services.Configure<ApiSettings>(Configuration.GetSection("ApiSettings"));
            services.AddTransient<IAPiSubscriberServices, APiSubscriberServices>();
            services.AddHttpClient<IAPiSubscriberServices, APiSubscriberServices>();
            // ... (otros servicios)

            // AutoMapper
            services.AddAutoMapper(typeof(Startup));

            // Identity con configuración personalizada
            services.AddIdentity<UserEntity, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // JWT Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["ValidIssuer"],
                    ValidAudience = jwtSettings["ValidAudience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            });

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy(_corsPolicy, builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyHeader()
                           .AllowAnyMethod()
                           .WithExposedHeaders("Content-Disposition");
                });
            });

            // Swagger
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            // Controllers
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseCors(_corsPolicy);
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
