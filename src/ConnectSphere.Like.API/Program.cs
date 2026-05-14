using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ConnectSphere.Like.Data;
using ConnectSphere.Like.Interfaces;
using ConnectSphere.Like.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Console.WriteLine("1 Starting Like API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    Console.WriteLine("2 Builder created");

    // ── Connection String ─────────────────────────────────────
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL not set.");

    Console.WriteLine("3 Connection string loaded");

    // ── Database ──────────────────────────────────────────────
    builder.Services.AddDbContext<LikeDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.EnableRetryOnFailure()));

    Console.WriteLine("4 DbContext registered");

    // ── JWT ───────────────────────────────────────────────────
    var jwtSecret =
        builder.Configuration["Jwt:Secret"]
        ?? Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? throw new InvalidOperationException("JWT_SECRET not configured.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    Console.WriteLine("5 JWT configured");

    // ── Services ──────────────────────────────────────────────
    builder.Services.AddScoped<ILikeService, LikeService>();

    Console.WriteLine("6 LikeService registered");

    // ── HttpClient ────────────────────────────────────────────
    builder.Services.AddHttpClient();

    var postServiceUrl =
        builder.Configuration["Services:PostService"]
        ?? Environment.GetEnvironmentVariable("POST_SERVICE_URL")
        ?? "http://localhost:5002";

    var notifServiceUrl =
        builder.Configuration["Services:NotifService"]
        ?? Environment.GetEnvironmentVariable("NOTIF_SERVICE_URL")
        ?? "http://localhost:5006";

    builder.Services.AddHttpClient("PostService", c =>
    {
        c.BaseAddress = new Uri(postServiceUrl);
    });

    builder.Services.AddHttpClient("NotifService", c =>
    {
        c.BaseAddress = new Uri(notifServiceUrl);
    });

    Console.WriteLine("7 HttpClients registered");

    // ── Swagger ───────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "ConnectSphere Like API",
            Version = "v1"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Bearer token",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddControllers();

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres");

    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()));

    Console.WriteLine("8 Swagger + Controllers added");

    // ── Build App ─────────────────────────────────────────────
    var app = builder.Build();

    Console.WriteLine("9 App built");

    // ── Migration ─────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            Console.WriteLine("10 Starting migration");

            var db = scope.ServiceProvider.GetRequiredService<LikeDbContext>();

            db.Database.Migrate();

            Console.WriteLine("11 Migration completed");

            Log.Information("Like database migration applied.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Migration failed");

            Log.Warning(ex, "Database migration skipped.");
        }
    }

    // ── Middleware ────────────────────────────────────────────
    app.UseSerilogRequestLogging();

    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Like API v1");
        c.RoutePrefix = string.Empty;
    });

    app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    Console.WriteLine("12 Starting server");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Like API failed to start.");
}
finally
{
    Log.CloseAndFlush();
}