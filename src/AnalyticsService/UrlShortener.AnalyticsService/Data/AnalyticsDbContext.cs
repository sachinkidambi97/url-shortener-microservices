using Microsoft.EntityFrameworkCore;
using UrlShortener.AnalyticsService.Data.Configurations;
using UrlShortener.AnalyticsService.Models;

namespace UrlShortener.AnalyticsService.Data;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
