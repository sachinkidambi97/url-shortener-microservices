using Microsoft.EntityFrameworkCore;
using UrlShortener.ShorteningApi.Models;

namespace UrlShortener.ShorteningApi.Data;

public sealed class ShorteningDbContext(DbContextOptions<ShorteningDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ShortenedUrl> ShortenedUrls => Set<ShortenedUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShorteningDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
