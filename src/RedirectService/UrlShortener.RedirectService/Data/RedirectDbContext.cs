using Microsoft.EntityFrameworkCore;
using UrlShortener.RedirectService.Data.Configurations;
using UrlShortener.RedirectService.Models;

namespace UrlShortener.RedirectService.Data;

/// <summary>
/// Read-only DbContext for the Redirect Service.
/// No migrations — the Shortening API owns the schema.
/// Query tracking is disabled globally.
/// </summary>
public sealed class RedirectDbContext(DbContextOptions<RedirectDbContext> options) : DbContext(options)
{
    public DbSet<ShortenedUrl> ShortenedUrls => Set<ShortenedUrl>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RedirectDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
