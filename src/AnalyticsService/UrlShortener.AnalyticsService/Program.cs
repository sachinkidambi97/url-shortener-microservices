using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using RabbitMQ.Client;
using Serilog;
using StackExchange.Redis;
using UrlShortener.AnalyticsService.Clients;
using UrlShortener.AnalyticsService.Data;
using UrlShortener.AnalyticsService.Repositories;
using UrlShortener.AnalyticsService.Services;
using UrlShortener.Shared.Extensions;
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

    // Controllers + Swagger
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "URL Shortener — Analytics API", Version = "v1" });
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
    builder.Services.AddDbContext<AnalyticsDbContext>(options =>
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
        return factory.CreateConnection("analytics-service");
    });

    // Options
    builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

    // JWT Authentication (shared extension)
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? throw new InvalidOperationException("JWT configuration is missing.");
    builder.Services.AddJwtAuthentication(jwtOptions);
    builder.Services.AddAuthorization();

    // HTTP client for Shortening API with Polly retry
    var shorteningApiBaseUrl = builder.Configuration["ShorteningApi:BaseUrl"]
        ?? "http://shortening-api:8080";

    var httpRetryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    builder.Services.AddHttpClient<ShorteningApiClient>(client =>
    {
        client.BaseAddress = new Uri(shorteningApiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    }).AddPolicyHandler(httpRetryPolicy);

    // Health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "db", tags: ["db"])
        .AddRedis(redisConnectionString, name: "redis", tags: ["redis"]);

    // Repositories & Services
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    builder.Services.AddScoped<IClickEventRepository, ClickEventRepository>();
    builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

    // RabbitMQ consumer background service
    builder.Services.AddHostedService<ClickEventConsumer>();

    var app = builder.Build();

    // Auto-migrate on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Analytics database migration applied successfully.");
    }

    // Middleware pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

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
