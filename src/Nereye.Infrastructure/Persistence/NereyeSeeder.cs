using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nereye.Application;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Persistence;
using Nereye.Infrastructure.Places;

namespace Nereye.Infrastructure.Persistence;

public static class NereyeSeeder
{
    /// <summary>Tohum verisini yazan örnekler aynı anda çalışırsa sırayla girsin (Postgres oturum kilidi).</summary>
    private const long SeedLockKey = 0x4E65726579650001;

    /// <summary>
    /// Şemayı günceller. Açılıştan ayrıdır: yayında tek bir adım olarak çalışır
    /// (<c>dotnet ef database update</c> ya da <c>dotnet Nereye.Api.dll migrate</c>; docs/veritabani-guncelleme.md).
    /// EF Core ayrıca migration sırasında veritabanı kilidi alır; iki adım aynı anda koşsa da çift uygulanmaz.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NereyeDbContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
    }

    /// <param name="migrate">
    /// Yalnızca geliştirmede true. Yayında şema ayrı adımda güncellenir; bekleyen migration varsa uygulama açılmaz
    /// (yarım şemayla çalışmasın, birden fazla örnek açılışta migration yarışına girmesin).
    /// </param>
    public static async Task SeedAsync(IServiceProvider services, bool migrate = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NereyeDbContext>();
        if (!db.Database.IsRelational())
        {
            await db.Database.EnsureCreatedAsync();
            await SeedDataAsync(scope.ServiceProvider, db);
            return;
        }

        if (migrate)
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Veritabanında uygulanmamış {pending.Count} migration var (son: {pending[^1]}). " +
                    "Önce şemayı güncelle: dotnet Nereye.Api.dll migrate (docs/veritabani-guncelleme.md).");
            }
        }

        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({SeedLockKey})");
            try
            {
                await SeedDataAsync(scope.ServiceProvider, db);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({SeedLockKey})");
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task SeedDataAsync(IServiceProvider provider, NereyeDbContext db)
    {

        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var name in new[] { RoleNames.Admin, RoleNames.User, RoleNames.VenueOwner })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                await roles.CreateAsync(new IdentityRole<Guid> { Name = name, Id = Guid.NewGuid() });
            }
        }

        var users = provider.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByEmailAsync("admin@nereye.app");
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = "admin@nereye.app",
                UserName = "admin@nereye.app",
                DisplayName = "Nereye Admin",
                EmailConfirmed = true
            };
            // Bilinen varsayılan şifre yalnızca geliştirmede. Production'da ADMIN_PASSWORD verilmezse
            // admin hesabı hiç oluşturulmaz: herkesin bildiği şifreyle açık bir yönetici kalmasın.
            var env = provider.GetRequiredService<IHostEnvironment>();
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(password) && !env.IsProduction())
            {
                password = "Admin!12345";
            }

            if (!string.IsNullOrWhiteSpace(password))
            {
                var created = await users.CreateAsync(admin, password);
                if (created.Succeeded)
                {
                    await users.AddToRolesAsync(admin, [RoleNames.Admin, RoleNames.User]);
                }
            }
        }

        if (!await db.Venues.AnyAsync())
        {
            var sureyya = FakePlacesClient.Istanbul.First(p => p.PlaceId == "nereye:sureyya");
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                GooglePlaceId = sureyya.PlaceId,
                PrimaryIntent = Intent.Kultur,
                Geohash = Domain.Geo.Geohash.Encode(sureyya.Latitude, sureyya.Longitude),
                CreatedAt = DateTime.UtcNow,
                Tags = [new VenueTag { Tag = VibeTag.Tarihi }]
            };
            venue.Events.Add(new CultureEvent
            {
                Id = Guid.NewGuid(),
                Title = "Klasik müzik gecesi",
                StartsAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(2).Date.AddHours(19), DateTimeKind.Utc),
                EndsAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(2).Date.AddHours(21), DateTimeKind.Utc)
            });
            // Demo: ana sayfadaki "Bu akşam" bandı boş kalmasın (bir sonraki yarım saatten bir saat sonra).
            var soon = DateTime.UtcNow.AddMinutes(90);
            soon = new DateTime(soon.Year, soon.Month, soon.Day, soon.Hour, soon.Minute < 30 ? 0 : 30, 0, DateTimeKind.Utc);
            venue.Events.Add(new CultureEvent
            {
                Id = Guid.NewGuid(),
                Title = "Akustik akşam",
                StartsAt = soon,
                EndsAt = soon.AddHours(2)
            });
            db.Venues.Add(venue);

            var petra = FakePlacesClient.Istanbul.First(p => p.PlaceId == "nereye:petra");
            db.Venues.Add(new Venue
            {
                Id = Guid.NewGuid(),
                GooglePlaceId = petra.PlaceId,
                PrimaryIntent = Intent.Kahve,
                Geohash = Domain.Geo.Geohash.Encode(petra.Latitude, petra.Longitude),
                CreatedAt = DateTime.UtcNow,
                Tags =
                [
                    new VenueTag { Tag = VibeTag.Laptop },
                    new VenueTag { Tag = VibeTag.Sessiz }
                ]
            });
            await db.SaveChangesAsync();
        }
    }
}
