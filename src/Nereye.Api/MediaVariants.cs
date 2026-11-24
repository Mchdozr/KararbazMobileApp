using System.Globalization;
using Microsoft.AspNetCore.StaticFiles;
using Nereye.Domain.Media;
using Nereye.Infrastructure.Places;

namespace Nereye.Api;

/// <summary>
/// <c>/media/venues/…</c> ve <c>/media/uploads/…</c> için <c>?w=</c> ile küçültülmüş sürüm. Kartta 1400 px'lik
/// özgün yerine 400/800 px iner. Önbellek kuralı statik dosyalarla aynıdır; ETag ile 304 desteklenir.
/// Sürüm üretilemezse istek statik dosyaya (özgün) düşer.
/// </summary>
public sealed class MediaVariantMiddleware
{
    private static readonly FileExtensionContentTypeProvider Types = new();
    private readonly RequestDelegate _next;

    public MediaVariantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, IWebHostEnvironment env, MediaVariantStore store)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!(HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method))
            || !ctx.Request.Query.TryGetValue("w", out var raw)
            || !int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var requested)
            || ImageVariants.Snap(requested) is not { } width
            || !ImageVariants.Supports(path))
        {
            await _next(ctx);
            return;
        }

        // Dosya sağlayıcısı kök dışına çıkışı (..) reddeder.
        var info = env.WebRootFileProvider.GetFileInfo(path);
        if (!info.Exists || info.IsDirectory || info.PhysicalPath is null)
        {
            await _next(ctx);
            return;
        }

        var variant = await store.GetAsync(new FileInfo(info.PhysicalPath), width, ctx.RequestAborted);
        if (variant is null)
        {
            await _next(ctx);
            return;
        }

        var file = new FileInfo(variant);
        var etag = $"\"{file.Length:x}-{file.LastWriteTimeUtc.Ticks:x}\"";
        var headers = ctx.Response.Headers;
        headers.ETag = etag;
        headers.CacheControl = MediaCachePolicy.For(path);
        if (EntityTags.Matches(ctx.Request.Headers.IfNoneMatch, etag))
        {
            ctx.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        ctx.Response.ContentType = Types.TryGetContentType(variant, out var type) ? type : "application/octet-stream";
        ctx.Response.ContentLength = file.Length;
        if (HttpMethods.IsHead(ctx.Request.Method))
        {
            return;
        }

        await ctx.Response.SendFileAsync(variant, ctx.RequestAborted);
    }
}

/// <summary>Statik medya ve küçültülmüş sürümleri için tek önbellek kuralı.</summary>
public static class MediaCachePolicy
{
    public static string For(string path) =>
        // Yorum fotoğrafı silinebilir (yorum silme, fotoğraf değişimi, moderasyon): uzun/immutable önbellek
        // silinmiş kareyi istemcide ve ara önbellekte yaşatırdı. Her istekte ETag ile doğrulanır (304 ucuzdur).
        path.StartsWith("/media/uploads/", StringComparison.Ordinal) ? "public, no-cache"
        : path.StartsWith("/media/avatars/", StringComparison.Ordinal) ? "public, max-age=300"
        : "public, max-age=86400";
}
