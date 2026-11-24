using Microsoft.EntityFrameworkCore;
using Nereye.Application.Common;
using Nereye.Application.Telemetry;
using Nereye.Domain.Entities;
using Nereye.Domain.Telemetry;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Telemetry;

public sealed class TelemetryService : ITelemetryService
{
    private static long _lastCleanupTicks;
    private readonly NereyeDbContext _db;
    private readonly IClock _clock;

    public TelemetryService(NereyeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RecordClientErrorsAsync(IReadOnlyList<ClientErrorReport> reports, CancellationToken cancellationToken = default)
    {
        if (reports.Count is 0 or > ClientErrorRules.MaxBatch)
        {
            throw new ValidationException("Geçersiz hata raporu.");
        }

        foreach (var r in reports)
        {
            var message = ErrorScrubber.Scrub(r.Message, ClientErrorRules.MessageMax);
            if (message.Length == 0)
            {
                continue;
            }

            var stack = ClientErrorRules.Clip(ErrorScrubber.Scrub(r.Stack, ClientErrorRules.StackMax), ClientErrorRules.StackMax);
            Upsert(
                "app",
                ClientErrorRules.Platform(r.Platform),
                ClientErrorRules.Clip(ErrorScrubber.Scrub(r.AppVersion), ClientErrorRules.VersionMax),
                ClientErrorRules.Clip(ErrorScrubber.Scrub(r.Screen), ClientErrorRules.ScreenMax),
                message,
                stack,
                r.Fatal);
        }

        await SaveAsync(cancellationToken);
    }

    public async Task RecordServerErrorAsync(Exception exception, string path, CancellationToken cancellationToken = default)
    {
        var message = ErrorScrubber.Scrub($"{exception.GetType().Name}: {exception.Message}", ClientErrorRules.MessageMax);
        var stack = ClientErrorRules.Clip(ErrorScrubber.Scrub(exception.StackTrace, ClientErrorRules.StackMax), ClientErrorRules.StackMax);
        Upsert("server", "server", null, ClientErrorRules.Clip(ErrorScrubber.Scrub(path), ClientErrorRules.ScreenMax), message, stack, false);
        await SaveAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorSummaryDto>> RecentErrorsAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = _clock.UtcNow.Date.AddDays(-Math.Clamp(days, 1, ClientErrorRules.RetentionDays) + 1);
        return await _db.ErrorReports
            .AsNoTracking()
            .Where(e => e.Day >= since)
            .OrderByDescending(e => e.Count)
            .ThenByDescending(e => e.LastSeenAt)
            .Take(200)
            .Select(e => new ErrorSummaryDto(e.Id, e.Day, e.Source, e.Platform, e.AppVersion, e.Screen, e.Message, e.Stack, e.Fatal, e.Count, e.LastSeenAt))
            .ToListAsync(cancellationToken);
    }

    private void Upsert(string source, string platform, string? version, string? screen, string message, string? stack, bool fatal)
    {
        var now = _clock.UtcNow;
        var day = now.Date;
        var fingerprint = ErrorFingerprint.Of(source, message, stack);
        var row = _db.ErrorReports.Local.FirstOrDefault(e => e.Fingerprint == fingerprint && e.Day == day)
                  ?? _db.ErrorReports.FirstOrDefault(e => e.Fingerprint == fingerprint && e.Day == day);
        if (row is null)
        {
            _db.ErrorReports.Add(new ErrorReport
            {
                Id = Guid.NewGuid(),
                Day = day,
                Fingerprint = fingerprint,
                Source = source,
                Platform = platform,
                AppVersion = version,
                Screen = screen,
                Message = message,
                Stack = stack,
                Fatal = fatal,
                Count = 1,
                FirstSeenAt = now,
                LastSeenAt = now
            });
            return;
        }

        row.Count++;
        row.LastSeenAt = now;
        row.Fatal |= fatal;
        row.AppVersion = version ?? row.AppVersion;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        // Saklama süresi: saatte en fazla bir kez eski satırlar silinir.
        var now = _clock.UtcNow;
        var last = Interlocked.Read(ref _lastCleanupTicks);
        if (now.Ticks - last > TimeSpan.TicksPerHour && Interlocked.CompareExchange(ref _lastCleanupTicks, now.Ticks, last) == last)
        {
            var cutoff = now.Date.AddDays(-ClientErrorRules.RetentionDays);
            var old = await _db.ErrorReports.Where(e => e.Day < cutoff).Take(500).ToListAsync(cancellationToken);
            _db.ErrorReports.RemoveRange(old);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Aynı parmak izini aynı anda yazan iki istek: tekil dizin ikincisini reddeder. Rapor kaybı kabul edilebilir.
            _db.ChangeTracker.Clear();
        }
    }
}
