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
    public static async Task SeedAsync(IServiceProvider services)
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

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var name in new[] { RoleNames.Admin, RoleNames.User, RoleNames.VenueOwner })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                await roles.CreateAsync(new IdentityRole<Guid> { Name = name, Id = Guid.NewGuid() });
            }
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
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
            await users.CreateAsync(admin, "Admin!12345");
            await users.AddToRolesAsync(admin, [RoleNames.Admin, RoleNames.User]);
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
