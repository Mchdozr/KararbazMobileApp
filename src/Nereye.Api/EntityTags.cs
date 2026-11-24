using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Nereye.Api;

/// <summary>Yanıt gövdesinden zayıf ETag üretimi ve If-None-Match karşılaştırması (RFC 9110, zayıf eşleşme).</summary>
public static class EntityTags
{
    public static string Compute(ReadOnlySpan<byte> body)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(body, hash);
        return "W/\"" + Convert.ToHexStringLower(hash[..12]) + "\"";
    }

    public static bool Matches(StringValues ifNoneMatch, string etag)
    {
        var mine = Opaque(etag);
        foreach (var header in ifNoneMatch)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part == "*" || Opaque(part) == mine)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Opaque(string tag) => tag.StartsWith("W/", StringComparison.Ordinal) ? tag[2..] : tag;
}

/// <summary>
/// GET yanıtına gövde özetinden ETag ekler; istemci aynı etiketi If-None-Match ile gönderirse gövdesiz 304 döner.
/// Cache-Control: private, no-cache — paylaşılan önbellekte saklanmaz, her açılışta sunucuya sorulur
/// (kullanıcıya göre değişen alanlar ve anlık açık/kapalı bilgisi var). Hesap her istekte yapılır;
/// kazanç mobil veride inmeyen gövdedir.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ETagAttribute : Attribute, IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method)
            && context.Result is ObjectResult { Value: { } value } result
            && (result.StatusCode ?? StatusCodes.Status200OK) == StatusCodes.Status200OK)
        {
            var json = http.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
            var body = JsonSerializer.SerializeToUtf8Bytes(value, result.DeclaredType ?? value.GetType(), json);
            var tag = EntityTags.Compute(body);
            http.Response.Headers.ETag = tag;
            http.Response.Headers.CacheControl = "private, no-cache";
            context.Result = EntityTags.Matches(http.Request.Headers.IfNoneMatch, tag)
                ? new StatusCodeResult(StatusCodes.Status304NotModified)
                : new FileContentResult(body, "application/json; charset=utf-8");
        }

        await next();
    }
}
