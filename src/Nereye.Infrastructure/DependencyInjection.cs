using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Admin;
using Nereye.Application.Auth;
using Nereye.Application.Claims;
using Nereye.Application.Common;
using Nereye.Application.Payments;
using Nereye.Application.Places;
using Nereye.Application.Preferences;
using Nereye.Application.Promotions;
using Nereye.Application.Recommendations;
using Nereye.Application.Reviews;
using Nereye.Application.Venues;
using Nereye.Infrastructure.Admin;
using Nereye.Infrastructure.Auth;
using Nereye.Infrastructure.Claims;
using Nereye.Infrastructure.Identity;
using Nereye.Infrastructure.Payments;
using Nereye.Infrastructure.Persistence;
using Nereye.Infrastructure.Places;
using Nereye.Infrastructure.Preferences;
using Nereye.Infrastructure.Promotions;
using Nereye.Infrastructure.Recommendations;
using Nereye.Infrastructure.Reviews;
using Nereye.Infrastructure.Venues;

namespace Nereye.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNereyeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<GooglePlacesOptions>(configuration.GetSection(GooglePlacesOptions.Section));

        var cs = configuration.GetConnectionString("Default") ?? "Host=localhost;Database=nereye;Username=postgres;Password=postgres";
        var useInMemory = configuration.GetValue("Database:UseInMemory", false);
        if (useInMemory)
        {
            services.AddDbContext<NereyeDbContext>(o => o.UseInMemoryDatabase(configuration["Database:Name"] ?? "nereye-tests"));
        }
        else
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            services.AddDbContext<NereyeDbContext>(o => o.UseNpgsql(cs));
        }

        services.AddHttpContextAccessor();
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<NereyeDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var redis = configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redis) || configuration.GetValue("Redis:UseMemory", false))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(o => o.Configuration = redis);
        }

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IPlacesCache, DistributedPlacesCache>();
        services.AddScoped<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IVenueQueryService, VenueQueryService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IPreferenceService, PreferenceService>();
        services.AddScoped<IClaimService, ClaimService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISavedPlaceService, SavedPlaceService>();
        services.AddSingleton<AvatarStorage>();
        services.AddSingleton<FakeGeoSuggest>();

        services.AddSingleton<FakePlacesClient>();
        services.AddHttpClient<GooglePlacesClient>();
        services.AddHttpClient<OverpassPlacesClient>();
        services.AddHttpClient<NominatimPlacesClient>();
        services.AddHttpClient<WikimediaPhotoClient>();
        services.AddHttpClient<WebsitePhotoClient>();
        services.AddScoped<IPlacePhotoEnricher, PlacePhotoEnricher>();
        services.AddScoped<IPlacesClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GooglePlacesOptions>>().Value;
            var fake = sp.GetRequiredService<FakePlacesClient>();
            var log = sp.GetService<ILogger<FallbackPlacesClient>>();
            if (options.UseFake)
            {
                return fake;
            }

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return new FallbackPlacesClient(
                    sp.GetRequiredService<GooglePlacesClient>(),
                    fake,
                    fallbackOnEmpty: true,
                    log);
            }

            var overpassThenFake = new FallbackPlacesClient(
                sp.GetRequiredService<OverpassPlacesClient>(),
                fake,
                fallbackOnEmpty: false,
                log);
            return new FallbackPlacesClient(
                sp.GetRequiredService<NominatimPlacesClient>(),
                overpassThenFake,
                fallbackOnEmpty: true,
                log);
        });
        services.AddScoped<IGeoSuggest>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GooglePlacesOptions>>().Value;
            return options.UseFake
                ? sp.GetRequiredService<FakeGeoSuggest>()
                : sp.GetRequiredService<NominatimPlacesClient>();
        });

        return services;
    }
}
