using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Admin;
using Nereye.Application.Auth;
using Nereye.Application.Claims;
using Nereye.Application.Common;
using Nereye.Application.Email;
using Nereye.Application.Payments;
using Nereye.Application.Places;
using Nereye.Application.Preferences;
using Nereye.Application.Promotions;
using Nereye.Application.Recommendations;
using Nereye.Application.Reviews;
using Nereye.Application.Routes;
using Nereye.Application.Venues;
using Nereye.Infrastructure.Admin;
using Nereye.Infrastructure.Auth;
using Nereye.Infrastructure.Claims;
using Nereye.Infrastructure.Email;
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
        // Dış servislere giden User-Agent'taki iletişim bilgisi: insan `Places:Contact` olarak verir.
        HttpIdentity.Configure(configuration["Places:Contact"] ?? Environment.GetEnvironmentVariable("NEREYE_CONTACT"));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<GooglePlacesOptions>(configuration.GetSection(GooglePlacesOptions.Section));
        services.PostConfigure<GooglePlacesOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.ApiKey))
            {
                o.ApiKey = Environment.GetEnvironmentVariable("GOOGLE_PLACES_API_KEY") ?? "";
            }
        });
        services.Configure<MapillaryOptions>(o =>
        {
            configuration.GetSection(MapillaryOptions.Section).Bind(o);
            if (string.IsNullOrWhiteSpace(o.Token))
            {
                o.Token = Environment.GetEnvironmentVariable("MAPILLARY_TOKEN");
            }
        });

        var cs = configuration.GetConnectionString("Default") ?? "Host=localhost;Database=nereye;Username=postgres;Password=postgres";
        var useInMemory = configuration.GetValue("Database:UseInMemory", false);
        if (useInMemory)
        {
            services.AddDbContext<NereyeDbContext>(o => o.UseInMemoryDatabase(configuration["Database:Name"] ?? "nereye-tests"));
        }
        else
        {
            // Zamanlar timestamptz (UTC) olarak tutulur; eski "legacy timestamp" anahtarı TimestampTz migration'ıyla kalktı.
            services.AddDbContext<NereyeDbContext>(o => o.UseNpgsql(cs));
        }

        services.AddHttpContextAccessor();
        services.AddIdentityCore<AppUser>(o =>
            {
                // Kural tek yerde: AccountRules (8+ karakter, harf + rakam). Sembol/büyük harf zorlamıyoruz.
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireDigit = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                o.Lockout.AllowedForNewUsers = true;
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
        services.AddMemoryCache();
        services.AddSingleton<IPlacesCache, DistributedPlacesCache>();
        services.AddSingleton<CacheRefresher>();
        // Sandbox ödeme yalnızca geliştirme ve testte. Başka ortamda tahsilat denemesi açıkça başarısız olur.
        services.AddScoped<IPaymentGateway>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            return env.IsDevelopment() || env.IsEnvironment("Testing")
                ? new SandboxPaymentGateway()
                : new UnavailablePaymentGateway();
        });
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.AddHttpClient<HttpEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
            return options.Complete ? sp.GetRequiredService<HttpEmailSender>() : new DisabledEmailSender();
        });
        // Hesap silme, şifre sıfırlama ve e-posta doğrulama bağlantıları kısa ömürlü.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IVenueQueryService, VenueQueryService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IPreferenceService, PreferenceService>();
        services.AddScoped<IClaimService, ClaimService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISavedPlaceService, SavedPlaceService>();
        services.AddScoped<Application.Telemetry.ITelemetryService, Telemetry.TelemetryService>();
        services.AddSingleton<AvatarStorage>();
        // Küçültülmüş görseller: yapılandırılmazsa geçici klasör (kaybolursa yeniden üretilir).
        services.AddSingleton(sp => new MediaVariantStore(
            configuration["Media:VariantCacheDir"] is { Length: > 0 } dir
                ? dir
                : Path.Combine(Path.GetTempPath(), "nereye-media-variants"),
            sp.GetService<ILogger<MediaVariantStore>>()));
        services.AddSingleton<PlaceCovers>();
        services.AddSingleton<FakeGeoSuggest>();

        services.AddSingleton<FakePlacesClient>();
        // Kendi OSRM/Overpass sunucularımız (docs/kendi-sunucular.md); boşsa genel sunucular.
        services.Configure<RoutingOptions>(configuration.GetSection(RoutingOptions.Section));
        services.Configure<OverpassOptions>(configuration.GetSection(OverpassOptions.Section));
        services.AddHttpClient<GooglePlacesClient>();
        services.AddHttpClient<OverpassPlacesClient>();
        // Public Nominatim: süreç genelinde saniyede en fazla bir istek (kullanım politikası).
        services.AddSingleton<NominatimGate>();
        services.AddHttpClient<NominatimPlacesClient>();
        services.AddHttpClient<WikimediaPhotoClient>();
        services.AddHttpClient<WebsitePhotoClient>();
        services.AddHttpClient<MapillaryPhotoClient>();
        services.AddHttpClient<OsrmRouteClient>();
        services.AddScoped<IRouteService, RouteService>();
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

            var overpass = sp.GetRequiredService<OverpassPlacesClient>();
            var nominatim = sp.GetRequiredService<NominatimPlacesClient>();
            var google = sp.GetRequiredService<GooglePlacesClient>();
            var cache = sp.GetRequiredService<IPlacesCache>();
            var osm = new FallbackPlacesClient(
                new CachedOsmPlacesClient(overpass, cache, sp.GetRequiredService<CacheRefresher>()),
                nominatim,
                fallbackOnEmpty: true,
                log);
            var nearby = new GoogleAugmentedPlacesClient(
                osm,
                google,
                options,
                sp.GetRequiredService<IPlacesCache>(),
                sp.GetService<ILogger<GoogleAugmentedPlacesClient>>());
            // Arama da aynı sarmalayıcıdan geçer: OSM boş dönerse ya da kapalıysa Google yanıtlar.
            var search = new GoogleAugmentedPlacesClient(
                new FallbackPlacesClient(nominatim, overpass, fallbackOnEmpty: true, log),
                google,
                options,
                sp.GetRequiredService<IPlacesCache>(),
                sp.GetService<ILogger<GoogleAugmentedPlacesClient>>());
            return new CompositePlacesClient(nearby, search, nearby);
        });
        // Popüler semtlerin OSM listesini önceden ısıtma. Sahte veriyle ve testte hiç çalışmaz.
        services.Configure<PlacesWarmupOptions>(configuration.GetSection(PlacesWarmupOptions.Section));
        if (configuration.GetValue("Places:Warmup:Enabled", false) && !configuration.GetValue("Places:UseFake", false))
        {
            services.AddHostedService(sp => new PlacesWarmupService(
                sp.GetRequiredService<IOptions<PlacesWarmupOptions>>().Value,
                sp.GetRequiredService<IPlacesCache>(),
                sp.GetRequiredService<CacheRefresher>(),
                sp.GetRequiredService<IClock>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetService<ILogger<PlacesWarmupService>>()));
        }

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
