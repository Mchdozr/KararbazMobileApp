using Nereye.Application.Common;

namespace Nereye.Api;

/// <summary>
/// Yakalanmamış hataları tek biçimli <c>{ error, requestId }</c> gövdesine çevirir.
/// İş kuralı mesajları istemciye geçer; beklenmeyen hataların ayrıntısı yalnızca loga yazılır.
/// </summary>
public sealed class ApiErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorMiddleware> _log;

    public ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // İstemci bağlantıyı kapattı; yazacak yanıt yok.
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var (status, message) = Map(ex, ctx);
            if (status >= 500)
            {
                _log.LogError(ex, "Unhandled {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            }
            else
            {
                _log.LogInformation("{Status} {Method} {Path}: {Message}", status, ctx.Request.Method, ctx.Request.Path, ex.Message);
            }

            ctx.Response.Clear();
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = message,
                requestId = ctx.Response.Headers["X-Request-Id"].ToString()
            });
        }
    }

    private static (int Status, string Message) Map(Exception ex, HttpContext ctx) => ex switch
    {
        AppException app => (app.StatusCode, app.Message),
        BadHttpRequestException bad => (bad.StatusCode, "Geçersiz istek."),
        UnauthorizedAccessException =>
            (ctx.User.Identity?.IsAuthenticated == true ? 403 : 401, Safe(ex, "Bu işlem için yetkin yok.")),
        ArgumentException => (400, Safe(ex, "Geçersiz istek.")),
        FormatException => (400, "Geçersiz istek."),
        InvalidOperationException when IsOurs(ex) => (400, ex.Message),
        _ => (500, "Sunucu hatası. Biraz sonra tekrar dene.")
    };

    // Kendi kodumuzun attığı mesajlar kullanıcıya dönüktür; çerçeve mesajları değildir.
    private static bool IsOurs(Exception ex) =>
        ex.TargetSite?.DeclaringType?.Assembly.GetName().Name?.StartsWith("Nereye", StringComparison.Ordinal) == true;

    private static string Safe(Exception ex, string fallback) => IsOurs(ex) ? StripParam(ex.Message) : fallback;

    private static string StripParam(string message)
    {
        var cut = message.IndexOf(" (Parameter", StringComparison.Ordinal);
        return cut > 0 ? message[..cut] : message;
    }
}

public static class ClientIp
{
    /// <summary>Hız sınırı bölümleme anahtarı: oturum varsa kullanıcı, yoksa IP.</summary>
    public static string Key(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst("sub")?.Value
                  ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return sub is not null ? "u:" + sub : "ip:" + Ip(ctx);
    }

    public static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
