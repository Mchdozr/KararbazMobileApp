namespace Nereye.Application.Telemetry;

public sealed record ClientErrorReport(
    string? Message,
    string? Stack,
    string? Screen,
    string? Platform,
    string? AppVersion,
    bool Fatal);

public sealed record ClientErrorBatch(IReadOnlyList<ClientErrorReport>? Reports);

public sealed record ErrorSummaryDto(
    Guid Id,
    DateTime Day,
    string Source,
    string Platform,
    string? AppVersion,
    string? Screen,
    string Message,
    string? Stack,
    bool Fatal,
    int Count,
    DateTime LastSeenAt);

public interface ITelemetryService
{
    Task RecordClientErrorsAsync(IReadOnlyList<ClientErrorReport> reports, CancellationToken cancellationToken = default);

    Task RecordServerErrorAsync(Exception exception, string path, CancellationToken cancellationToken = default);

    /// <summary>Son günlerin hataları, en çok tekrar edenden aza.</summary>
    Task<IReadOnlyList<ErrorSummaryDto>> RecentErrorsAsync(int days, CancellationToken cancellationToken = default);
}
