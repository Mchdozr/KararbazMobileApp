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

    public TelemetryController(ITelemetryService telemetry) => _telemetry = telemetry;

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
