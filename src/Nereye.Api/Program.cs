using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("osm", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 90;
        opt.QueueLimit = 0;
    });
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
                  ?? ["https://nereye.app"];
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
var signing = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY");
if (!string.IsNullOrWhiteSpace(signing))
{
    jwt.SigningKey = signing;
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
await NereyeSeeder.SeedAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (ctx, next) =>
{
    var id = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Response.Headers["X-Request-Id"] = id;
    await next();
});
app.UseCors();
if (!testing)
{
    app.UseRateLimiter();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
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
