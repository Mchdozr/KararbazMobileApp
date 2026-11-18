using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Nereye.Application.Email;

namespace Nereye.Infrastructure.Email;

/// <summary>
/// E-posta sağlayıcı ayarları. Değerler yalnızca ortamdan gelir (ör. <c>Email__ApiKey</c>); repoda tutulmaz.
/// Provider: "resend" ya da "postmark". From: sağlayıcıda doğrulanmış gönderici adresi.
/// </summary>
public sealed class EmailOptions
{
    public const string Section = "Email";
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? From { get; set; }

    public bool Complete =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(From)
        && Provider?.Trim().ToLowerInvariant() is "resend" or "postmark";
}

/// <summary>Sağlayıcı yok: hiçbir şey göndermez, akışlar "kapalı" yanıtı verir.</summary>
public sealed class DisabledEmailSender : IEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("E-posta sağlayıcısı tanımlı değil.");
}

/// <summary>Resend ya da Postmark HTTP API'si. Gerçek hesapla denenmedi (docs/yayin-riskleri.md).</summary>
public sealed class HttpEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;

    public HttpEmailSender(HttpClient http, IOptions<EmailOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public bool IsConfigured => _options.Complete;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("E-posta sağlayıcısı tanımlı değil.");
        }

        using var request = _options.Provider!.Trim().ToLowerInvariant() == "postmark"
            ? Postmark(message)
            : Resend(message);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage Resend(EmailMessage m)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new { from = _options.From, to = new[] { m.To }, subject = m.Subject, html = m.Html, text = m.Text })
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _options.ApiKey);
        return request;
    }

    private HttpRequestMessage Postmark(EmailMessage m)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.postmarkapp.com/email")
        {
            Content = JsonContent.Create(new
            {
                From = _options.From,
                To = m.To,
                Subject = m.Subject,
                HtmlBody = m.Html,
                TextBody = m.Text,
                MessageStream = "outbound"
            })
        };
        request.Headers.TryAddWithoutValidation("X-Postmark-Server-Token", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        return request;
    }
}
