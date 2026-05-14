using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ConnectSphere.Post.Data;
using ConnectSphere.Post.Interfaces;
using ConnectSphere.Post.Services;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console());

    // ── Database ───────────────────────────────────────────────────────────────
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL not set.");

    builder.Services.AddDbContext<PostDbContext>(options => options.UseNpgsql(connectionString));

    // ── MassTransit + RabbitMQ ─────────────────────────────────────────────────
    var rabbitHost = builder.Configuration["RabbitMQ:Host"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
    var rabbitUser = builder.Configuration["RabbitMQ:User"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
    var rabbitPass = builder.Configuration["RabbitMQ:Pass"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest";

    builder.Services.AddMassTransit(x =>
    {
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
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // ── Services ───────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IPostService, PostService>();

    // ── Swagger & Controllers ──────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "ConnectSphere Post API", Version = "v1" });
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
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres");
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PostDbContext>();
        db.Database.Migrate();
        Log.Information("Post database migration applied.");
    }

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Post API v1"); c.RoutePrefix = string.Empty; });
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Post API failed to start."); }
finally { Log.CloseAndFlush(); }