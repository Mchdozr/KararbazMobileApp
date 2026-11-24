using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nereye.Application;
using Nereye.Application.Admin;
using Nereye.Application.Claims;

namespace Nereye.Api.Controllers;

[Authorize(Roles = RoleNames.Admin)]
[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly IClaimService _claims;

    public AdminController(IAdminService admin, IClaimService claims)
    {
        _admin = admin;
        _claims = claims;
    }

    /// <summary>Bu örneğin son 15 dakikası: uç başına p50/p95/p99, dış servis hata oranı, önbellek isabeti.</summary>
    [HttpGet("metrics")]
    public ActionResult<Application.Telemetry.MetricsSnapshot> Metrics([FromServices] Infrastructure.Telemetry.MetricsCollector collector) =>
        Ok(collector.Snapshot());

    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportAdminDto>>> Reports([FromQuery] bool unresolvedOnly = true, CancellationToken ct = default) =>
        Ok(await _admin.ListReportsAsync(unresolvedOnly, ct));

    [HttpPost("reports/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromQuery] bool hideReview = false, CancellationToken ct = default)
    {
        await _admin.ResolveReportAsync(id, hideReview, ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/ban")]
    public async Task<IActionResult> Ban(Guid id, [FromQuery] bool banned = true, CancellationToken ct = default)
    {
        await _admin.BanUserAsync(id, banned, ct);
        return NoContent();
    }

    [HttpGet("tags")]
    public ActionResult<IReadOnlyList<TagDictionaryItem>> Tags() => Ok(_admin.Tags());

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserAdminDto>>> Users(CancellationToken ct) =>
        Ok(await _admin.ListUsersAsync(ct));

    [HttpPost("claims/{id:guid}/approve")]
    public async Task<IActionResult> ApproveClaim(Guid id, CancellationToken ct)
    {
        await _claims.ApproveAsync(id, ct);
        return NoContent();
    }

    [HttpPost("claims/{id:guid}/reject")]
    public async Task<IActionResult> RejectClaim(Guid id, CancellationToken ct)
    {
        await _claims.RejectAsync(id, ct);
        return NoContent();
    }
}
