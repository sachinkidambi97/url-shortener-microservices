using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UrlShortener.RedirectService.Models;

namespace UrlShortener.RedirectService.Data.Configurations;

public sealed class ShortenedUrlConfiguration : IEntityTypeConfiguration<ShortenedUrl>
{
    public void Configure(EntityTypeBuilder<ShortenedUrl> builder)
    {
        builder.ToTable("shortened_urls");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.ShortCode).HasColumnName("short_code").HasMaxLength(30).IsRequired();
        builder.Property(u => u.OriginalUrl).HasColumnName("original_url").IsRequired();
        builder.Property(u => u.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(u => u.ShortCode).IsUnique();
    }
}
