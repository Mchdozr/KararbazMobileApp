using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nereye.Infrastructure.Identity;

namespace Nereye.Infrastructure.Persistence;

public sealed class NereyeDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public NereyeDbContext(DbContextOptions<NereyeDbContext> options) : base(options)
    {
    }

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueTag> VenueTags => Set<VenueTag>();
    public DbSet<VenuePhoto> VenuePhotos => Set<VenuePhoto>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewTag> ReviewTags => Set<ReviewTag>();
    public DbSet<ReviewPhoto> ReviewPhotos => Set<ReviewPhoto>();
    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<CultureEvent> CultureEvents => Set<CultureEvent>();
    public DbSet<VenueClaim> VenueClaims => Set<VenueClaim>();
    public DbSet<PromotionCampaign> PromotionCampaigns => Set<PromotionCampaign>();
    public DbSet<PromotionImpression> PromotionImpressions => Set<PromotionImpression>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserPreferenceIntent> UserPreferenceIntents => Set<UserPreferenceIntent>();
    public DbSet<UserPreferenceVibe> UserPreferenceVibes => Set<UserPreferenceVibe>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ConsentLog> ConsentLogs => Set<ConsentLog>();
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();

    /// <summary>
    /// Her DateTime UTC'dir: yazarken türü belirsiz değer UTC sayılır, yerel saat UTC'ye çevrilir; okurken Kind=Utc.
    /// Npgsql timestamptz sütununa yalnızca UTC kabul eder; bu dönüştürücü yanlış türdeki bir değerin yazmayı düşürmesini önler.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Venue>(e =>
        {
            e.HasIndex(x => x.GooglePlaceId).IsUnique();
            e.Property(x => x.GooglePlaceId).HasMaxLength(128);
            e.Property(x => x.Geohash).HasMaxLength(16);
        });

        builder.Entity<VenueTag>().HasKey(x => new { x.VenueId, x.Tag });
        builder.Entity<ReviewTag>().HasKey(x => new { x.ReviewId, x.Tag });
        builder.Entity<Favorite>().HasKey(x => new { x.UserId, x.VenueId });
        builder.Entity<UserPreference>().HasKey(x => x.UserId);
        builder.Entity<UserPreferenceIntent>().HasKey(x => new { x.UserId, x.Intent });
        builder.Entity<UserPreferenceVibe>().HasKey(x => new { x.UserId, x.Tag });
        builder.Entity<UserPreference>()
            .HasMany(x => x.Intents)
            .WithOne()
            .HasForeignKey(x => x.UserId);
        builder.Entity<UserPreference>()
            .HasMany(x => x.Vibes)
            .WithOne()
            .HasForeignKey(x => x.UserId);

        builder.Entity<Review>()
            .HasOne(x => x.Reply)
            .WithOne()
            .HasForeignKey<ReviewReply>(x => x.ReviewId);

        builder.Entity<Venue>()
            .HasMany(x => x.Tags)
            .WithOne(x => x.Venue)
            .HasForeignKey(x => x.VenueId);

        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash);
        // UPDATE ... WHERE "Revoked" = false: aynı token'la gelen iki yenilemeden yalnızca biri kazanır.
        builder.Entity<RefreshToken>().Property(x => x.Revoked).IsConcurrencyToken();
        // Sık çalışan sorguların taradığı sütunlar: "yorumlarım", oturum temizliği, trend kayıtlar,
        // açık şikayet kontrolü ve etkin kampanya araması.
        builder.Entity<RefreshToken>().HasIndex(x => x.UserId);
        builder.Entity<Review>().HasIndex(x => x.UserId);
        builder.Entity<Favorite>().HasIndex(x => new { x.VenueId, x.CreatedAt });
        builder.Entity<Report>().HasIndex(x => new { x.ReporterUserId, x.IsResolved });
        builder.Entity<PromotionCampaign>().HasIndex(x => new { x.Status, x.TargetIntent, x.EndAt });
        builder.Entity<ConsentLog>().HasIndex(x => x.UserId);
        builder.Entity<UserBlock>().HasKey(x => new { x.BlockerUserId, x.BlockedUserId });
        builder.Entity<Report>().Property(x => x.PhotoUrl).HasMaxLength(200);
        builder.Entity<SavedPlace>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Label).HasMaxLength(40);
            e.Property(x => x.Kind).HasMaxLength(16);
        });
    }
}
