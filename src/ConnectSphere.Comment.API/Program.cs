using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ConnectSphere.Comment.Data;
using ConnectSphere.Comment.Interfaces;
using ConnectSphere.Comment.Services;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DATABASE_URL")
        ?? throw new InvalidOperationException("DATABASE_URL not set.");

    builder.Services.AddDbContext<CommentDbContext>(o => o.UseNpgsql(connectionString));

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? Environment.GetEnvironmentVariable("JWT_SECRET")
        ?? throw new InvalidOperationException("JWT_SECRET not configured.");

    // builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    //     .AddJwtBearer(o =>
    //     {
    //         o.TokenValidationParameters = new TokenValidationParameters
    //         {
    //             ValidateIssuerSigningKey = true,
    //             IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    //             ValidateIssuer = false, ValidateAudience = false, ClockSkew = TimeSpan.Zero
    //         };
    //     });
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret)
        ),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

    builder.Services.AddAuthorization();
    builder.Services.AddScoped<ICommentService, CommentService>();

    var postServiceUrl = Environment.GetEnvironmentVariable("POST_SERVICE_URL") ?? "http://localhost:5002";
    var notifServiceUrl = Environment.GetEnvironmentVariable("NOTIF_SERVICE_URL") ?? "http://localhost:5006";

    builder.Services.AddHttpClient("PostService", c => c.BaseAddress = new Uri(postServiceUrl));
    builder.Services.AddHttpClient("NotifService", c => c.BaseAddress = new Uri(notifServiceUrl));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "ConnectSphere Comment API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Description = "JWT Bearer", Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey, Scheme = "Bearer" });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
    });

    builder.Services.AddControllers();
    builder.Services.AddHealthChecks().AddNpgSql(connectionString, name: "postgres");
    builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader());
});
    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
        db.Database.Migrate();
    }

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Comment API v1"); c.RoutePrefix = string.Empty; });
    app.UseCors("AllowFrontend");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Comment API failed to start."); }
finally { Log.CloseAndFlush(); }