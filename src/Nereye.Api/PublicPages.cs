using System.Net;

namespace Nereye.Api;

/// <summary>API'nin kendi hostunda sunulan sade web sayfaları (hesap silme, gizlilik, şifre sıfırlama).</summary>
public static class PublicPages
{
    public static string Render(string title, string body, string? head = null) => $$"""
        <!doctype html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex">
        <title>{{Encode(title)}} · Kararbaz</title>
        <style>
          :root { --bg:#fbf6ee; --ink:#064e3b; --muted:#3f6f5c; --line:#ebe3d2; --danger:#a8402b; }
          * { box-sizing: border-box; }
          body { margin:0; background:var(--bg); color:var(--ink); font:16px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif; }
          main { max-width: 640px; margin: 0 auto; padding: 32px 16px 48px; }
          h1 { font-size: 28px; line-height: 1.2; margin: 0 0 12px; }
          h2 { font-size: 18px; margin: 24px 0 8px; }
          p, li { color: var(--ink); }
          .muted { color: var(--muted); font-size: 14px; }
          .card { background:#fffdf8; border:1px solid var(--line); border-radius:16px; padding:16px; margin-top:16px; }
          label { display:block; font-weight:600; margin-bottom:6px; }
          input { width:100%; min-height:48px; border:1.5px solid var(--line); border-radius:12px; padding:0 12px; font-size:16px; background:#fff; color:var(--ink); }
          button { margin-top:12px; min-height:48px; padding:0 18px; border:0; border-radius:12px; font-size:16px; font-weight:700; color:#fff; background:var(--ink); cursor:pointer; }
          button.danger { background: var(--danger); }
          .note { border-left:3px solid var(--danger); padding-left:12px; }
        </style>
        {{head ?? ""}}
        </head>
        <body><main>
        <h1>{{Encode(title)}}</h1>
        {{body}}
        </main></body>
        </html>
        """;

    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "");

    public static IResult Html(string title, string body, int status = 200, string? head = null) =>
        Results.Content(Render(title, body, head), "text/html; charset=utf-8", statusCode: status);

    /// <summary>
    /// E-postadaki bağlantıların kökü. Yayında yalnızca insanın verdiği <c>Api:PublicBaseUrl</c> (https);
    /// istekteki Host başlığına güvenilmez (bağlantı saldırganın alan adına yönlendirilemesin).
    /// Geliştirme ve testte isteğin kendi adresi kullanılır. Yoksa null: bağlantı gönderen akışlar kapalıdır.
    /// </summary>
    public static string? BaseUrl(HttpContext ctx, IConfiguration config, IHostEnvironment env)
    {
        var configured = config["Api:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            return Uri.TryCreate(configured, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? configured : null;
        }

        return env.IsDevelopment() || env.IsEnvironment("Testing")
            ? $"{ctx.Request.Scheme}://{ctx.Request.Host}"
            : null;
    }
}
