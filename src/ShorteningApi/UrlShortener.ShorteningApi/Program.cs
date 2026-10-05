using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using UrlShortener.Shared.Extensions;
using UrlShortener.Shared.Health;
using UrlShortener.Shared.Infrastructure;
using UrlShortener.Shared.Middleware;
using UrlShortener.Shared.Services;
using UrlShortener.ShorteningApi.Data;
using UrlShortener.ShorteningApi.Repositories;
using UrlShortener.ShorteningApi.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // Controllers + Swagger
    builder.Services.AddControllers();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "URL Shortener — Shortening API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // EF Core with PostgreSQL
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<ShorteningDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Redis
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));

    // Options
    builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

    // JWT Authentication (shared extension)
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("JWT configuration is missing.");
    builder.Services.AddJwtAuthentication(jwtOptions);
    builder.Services.AddAuthorization();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "db", tags: ["db"])
        .AddRedis(redisConnectionString, name: "redis", tags: ["redis"]);

    // Repositories
    builder.Services.AddScoped<IUrlRepository, UrlRepository>();
    builder.Services.AddScoped<IUserRepository, UserRepository>();

    // Services
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IUrlShorteningService, UrlShorteningService>();

    // Background services
    builder.Services.AddHostedService<ExpiredUrlCleanupService>();

    // Rate limiting: fixed window per IP
    var rateLimitPermit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
    var rateLimitWindowMinutes = builder.Configuration.GetValue("RateLimiting:WindowMinutes", 1);
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("fixed", limiterOptions =>
        {
            limiterOptions.PermitLimit = rateLimitPermit;
            limiterOptions.Window = TimeSpan.FromMinutes(rateLimitWindowMinutes);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
        options.OnRejected = async (ctx, token) =>
        {
            ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Rate limit exceeded. Please try again later."
            }, token);
        };
    });

    var app = builder.Build();

    // Auto-migrate on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ShorteningDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migration applied successfully.");
    }

    // Middleware pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseCors("AllowFrontend");

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteResponse
    });

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
