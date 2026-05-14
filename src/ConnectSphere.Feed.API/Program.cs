using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ConnectSphere.Feed.Consumers;
using ConnectSphere.Feed.Data;
using ConnectSphere.Feed.Interfaces;
using ConnectSphere.Feed.Services;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

    // ── Database ───────────────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL not set.");

    builder.Services.AddDbContext<FeedDbContext>(o => o.UseNpgsql(connectionString));

    // ── In-Memory Cache (Replacing Redis) ──────────────────────────────────────
    builder.Services.AddDistributedMemoryCache();

    // ── MassTransit + RabbitMQ ─────────────────────────────────────────────────
    var rabbitHost = builder.Configuration["RabbitMQ:Host"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
    var rabbitUser = builder.Configuration["RabbitMQ:User"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
    var rabbitPass = builder.Configuration["RabbitMQ:Pass"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

    builder.Services.AddMassTransit(x =>
    {
        // Register the PostCreatedEvent consumer
        x.AddConsumer<PostCreatedConsumer>();

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(rabbitHost, "mqhtvubt", h =>
            {
                h.Username(rabbitUser);
                h.Password(rabbitPass);
            });
            cfg.ConfigureEndpoints(ctx);
        });
    });

    // ── JWT ────────────────────────────────────────────────────────────────────
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? throw new InvalidOperationException("JWT_SECRET not configured.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false, ValidateAudience = false, ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddScoped<IFeedService, FeedService>();

    // ── HTTP Clients for inter-service calls ───────────────────────────────────
    var authUrl   = Environment.GetEnvironmentVariable("AUTH_SERVICE_URL")   ?? "http://localhost:5001";
    var postUrl   = Environment.GetEnvironmentVariable("POST_SERVICE_URL")   ?? "http://localhost:5002";
    var followUrl = Environment.GetEnvironmentVariable("FOLLOW_SERVICE_URL") ?? "http://localhost:5005";

    builder.Services.AddHttpClient("AuthService",   c => c.BaseAddress = new Uri(authUrl));
    builder.Services.AddHttpClient("PostService",   c => c.BaseAddress = new Uri(postUrl));
    builder.Services.AddHttpClient("FollowService", c => c.BaseAddress = new Uri(followUrl));

    // ── Swagger & Controllers ──────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "ConnectSphere Feed API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Bearer", Name = "Authorization",
            In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey, Scheme = "Bearer"
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres");
        // .AddRedis(redisConnection, name: "redis");

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FeedDbContext>();
        db.Database.Migrate();
    }

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Feed API v1"); c.RoutePrefix = string.Empty; });
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Feed API failed to start."); }
finally { Log.CloseAndFlush(); }