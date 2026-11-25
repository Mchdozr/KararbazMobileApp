using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nereye.Application.Common;
using Nereye.Application.Telemetry;
using Nereye.Domain.Telemetry;

namespace Nereye.Api.Controllers;

/// <summary>
/// Uygulamanın hata raporları. Oturum gerekmez ve kullanıcıya bağlanmaz; metinler sunucuda kişisel veriden
/// yeniden ayıklanır (ErrorScrubber). Gün ve parmak izi başına tek satır tutulur, 30 gün saklanır.
/// </summary>
[ApiController]
[Route("api/telemetry")]
[AllowAnonymous]
[EnableRateLimiting("telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetry;
    private readonly Nereye.Application.Analytics.IAnalyticsService _analytics;

    public TelemetryController(ITelemetryService telemetry, Nereye.Application.Analytics.IAnalyticsService analytics)
    {
        _telemetry = telemetry;
        _analytics = analytics;
    }

    /// <summary>
    /// Kullanım olayları. İstemci yalnızca kullanıcı analitiğe izin verdiyse gönderir; olaylar kimlik ve konum
    /// taşımaz, gün x olay x (mekan ya da niyet) sayacına eklenir. Bilinmeyen olaylar sessizce atlanır.
    /// </summary>
    [HttpPost("events")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<IActionResult> Events(Nereye.Application.Analytics.AnalyticsBatch batch, CancellationToken ct)
    {
        if (batch.Events is not { Count: > 0 and <= Nereye.Domain.Analytics.AnalyticsRules.MaxBatch } events)
        {
            throw new ValidationException("Geçersiz olay paketi.");
        }

        await _analytics.RecordAsync(events, ct);
        return Accepted();
    }

    [HttpPost("errors")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Errors(ClientErrorBatch batch, CancellationToken ct)
    {
        if (batch.Reports is not { Count: > 0 and <= ClientErrorRules.MaxBatch } reports)
        {
            throw new ValidationException("Geçersiz hata raporu.");
        }

        await _telemetry.RecordClientErrorsAsync(reports, ct);
        return Accepted();
    }
}
