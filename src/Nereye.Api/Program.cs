using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Nereye.Api;
using Nereye.Infrastructure;
using Nereye.Infrastructure.Auth;
using Nereye.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNereyeInfrastructure(builder.Configuration);
builder.Services.PostConfigure<JwtOptions>(o =>
{
    var envKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
    if (!string.IsNullOrWhiteSpace(envKey))
    {
        o.SigningKey = envKey;
    }
});
// Sandbox ödeme gibi yalnızca geliştirmede anlamlı uçlar yayında hiç yönlendirilmez (404).
var devSurface = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
builder.Services.AddControllers(o => o.Conventions.Add(new DevelopmentOnlyConvention(devSurface))).AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    // Model bağlama hataları da diğer hatalarla aynı { error } biçiminde dönsün.
    o.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new { error = "Geçersiz istek." });
});
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
// Yalnızca ters vekil arkasında aç (Proxy:TrustForwardedHeaders=true); aksi halde
// istemci X-Forwarded-For ile IP'sini uydurup hız sınırını aşabilir.
var trustProxy = builder.Configuration.GetValue("Proxy:TrustForwardedHeaders", false);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Çok sık istek. Bir dakika sonra tekrar dene." }, ct);
    };
    // Sınırlar istemci başına: tek bir kullanıcı herkesin kotasını tüketemez.
    o.AddPolicy("osm", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Key(ctx), _ => new FixedWindowRateLimiterOptions
    {
        Window = TimeSpan.FromMinutes(1),
        PermitLimit = 90,
        QueueLimit = 0
    }));
    // Konum önerisi: sunucu Nominatim'e zaten saniyede en fazla bir istek gönderir ve sonucu önbelleğe alır;
    // bu sınır tek bir istemcinin kuyruğu doldurmasını engeller.
    o.AddPolicy("geo", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Key(ctx), _ => new FixedWindowRateLimiterOptions
    {
        Window = TimeSpan.FromMinutes(1),
        PermitLimit = 30,
        QueueLimit = 0
    }));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter("ip:" + ClientIp.Ip(ctx), _ => new FixedWindowRateLimiterOptions
    {
        Window = TimeSpan.FromMinutes(1),
        PermitLimit = 12,
        QueueLimit = 0
    }));
    o.AddPolicy("write", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp.Key(ctx), _ => new FixedWindowRateLimiterOptions
    {
        Window = TimeSpan.FromMinutes(1),
        PermitLimit = 30,
        QueueLimit = 0
    }));
});

var testing = builder.Environment.IsEnvironment("Testing");
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (testing || builder.Environment.IsDevelopment())
    {
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        return;
    }

    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  // Mobil uygulama CORS kullanmaz; tarayıcıdan erişecek kökenleri insan `Cors:Origins` ile verir.
                  ?? [];
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var signing = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (!string.IsNullOrWhiteSpace(signing))
{
    jwt.SigningKey = signing;
}

if (builder.Environment.IsProduction()
    && (jwt.SigningKey.Length < 32 || jwt.SigningKey.StartsWith("dev-only", StringComparison.Ordinal)))
{
    throw new InvalidOperationException("Production için JWT_SIGNING_KEY ortam değişkeni (en az 32 karakter) zorunlu.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment() && !testing;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
        };
    });

var app = builder.Build();
if (args.Contains("migrate"))
{
    // Ayrı yayın adımı: şemayı güncelle ve çık (docs/veritabani-guncelleme.md).
    await NereyeSeeder.MigrateAsync(app.Services);
    return;
}

// Açılışta migration yalnızca geliştirmede; yayında ayrı adımdır (çoklu örnek yarışmasın).
await NereyeSeeder.SeedAsync(app.Services, app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (trustProxy)
{
    app.UseForwardedHeaders();
}

app.Use(async (ctx, next) =>
{
    var given = ctx.Request.Headers["X-Request-Id"].FirstOrDefault();
    var id = given is { Length: > 0 and <= 64 } && given.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
        ? given
        : Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Request-Id"] = id;
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
    {
        await next();
    }
});
app.UseMiddleware<ApiErrorMiddleware>();
if (app.Environment.IsProduction())
{
    app.UseHsts();
}

app.UseResponseCompression();
app.UseCors();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Yüklenen dosyalar benzersiz adlıdır; mekan görselleri nadiren değişir.
        var path = ctx.Context.Request.Path.Value ?? "";
        ctx.Context.Response.Headers.CacheControl =
            path.StartsWith("/media/uploads/", StringComparison.Ordinal) ? "public, max-age=31536000, immutable"
            : path.StartsWith("/media/avatars/", StringComparison.Ordinal) ? "public, max-age=300"
            : "public, max-age=86400";
    }
});
app.UseAuthentication();
if (!testing)
{
    // Kimlik doğrulamadan sonra: bölümleme anahtarı kullanıcıyı görebilsin.
    app.UseRateLimiter();
}

app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health", async (NereyeDbContext db, IDistributedCache cache) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    try
    {
        await cache.SetStringAsync("health:ping", "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15)
        });
    }
    catch
    {
        return Results.Json(new { status = "fail", db = dbOk, cache = false }, statusCode: 503);
    }

    return dbOk
        ? Results.Ok(new { status = "ok", db = true, cache = true })
        : Results.Json(new { status = "fail", db = false, cache = true }, statusCode: 503);
});
app.Run();

public partial class Program;
