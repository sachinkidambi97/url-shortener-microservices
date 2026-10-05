using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;
using UrlShortener.RedirectService.Data;
using UrlShortener.RedirectService.Services;
using UrlShortener.Shared.Health;
using UrlShortener.Shared.Infrastructure;
using UrlShortener.Shared.Middleware;
using UrlShortener.Shared.Services;

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

    // EF Core — read-only, NO migrations
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<RedirectDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Redis
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));

    // RabbitMQ
    var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq";
    var rabbitPort = builder.Configuration.GetValue("RabbitMQ:Port", 5672);
    var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
    var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

    builder.Services.AddSingleton<IConnection>(_ =>
    {
        var factory = new ConnectionFactory
        {
            HostName = rabbitHost,
            Port = rabbitPort,
            UserName = rabbitUser,
            Password = rabbitPass,
            AutomaticRecoveryEnabled = true
        };
        return factory.CreateConnection("redirect-service");
    });

    // Options
    builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "db", tags: ["db"])
        .AddRedis(redisConnectionString, name: "redis", tags: ["redis"]);

    // Services
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IClickEventPublisher, ClickEventPublisher>();
    builder.Services.AddScoped<IRedirectService, RedirectService>();

    var app = builder.Build();

    // Middleware pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseCors("AllowFrontend");

    app.UseSerilogRequestLogging();

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
