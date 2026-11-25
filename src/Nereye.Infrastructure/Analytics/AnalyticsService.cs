using Microsoft.EntityFrameworkCore;
using Nereye.Application.Analytics;
using Nereye.Application.Common;
using Nereye.Domain.Analytics;
using Nereye.Domain.Entities;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Analytics;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly NereyeDbContext _db;
    private readonly IClock _clock;

    public AnalyticsService(NereyeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task RecordAsync(IReadOnlyList<AnalyticsEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count is 0 or > AnalyticsRules.MaxBatch)
        {
            throw new ValidationException("Geçersiz olay paketi.");
        }

        var day = _clock.UtcNow.Date;
        var grouped = events
            .Select(e => AnalyticsRules.TryKey(e.Name, e.PlaceId, e.Intent, out var key) ? (e.Name!, key) : default)
            .Where(x => x != default)
            .GroupBy(x => x)
            .ToList();
        if (grouped.Count == 0)
        {
            return;
        }

        var names = grouped.Select(g => g.Key.Item1).Distinct().ToList();
        var keys = grouped.Select(g => g.Key.key).Distinct().ToList();
        var existing = await _db.DailyCounts
            .Where(c => c.Day == day && names.Contains(c.Name) && keys.Contains(c.Key))
            .ToListAsync(cancellationToken);
        foreach (var g in grouped)
        {
            var row = existing.FirstOrDefault(c => c.Name == g.Key.Item1 && c.Key == g.Key.key);
            if (row is null)
            {
                _db.DailyCounts.Add(new DailyCount { Id = Guid.NewGuid(), Day = day, Name = g.Key.Item1, Key = g.Key.key, Count = g.Count() });
            }
            else
            {
                row.Count += g.Count();
            }
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Aynı sayacı aynı anda ilk kez yazan iki istek: biri kaybolur. Analitik için kabul edilebilir.
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<VenueStatsDto> CountsAsync(string placeId, int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 90);
        var today = _clock.UtcNow.Date;
        var since = today.AddDays(-days + 1);
        var rows = await _db.DailyCounts
            .AsNoTracking()
            .Where(c => c.Key == placeId && c.Day >= since)
            .ToListAsync(cancellationToken);

        var venueId = await _db.Venues.Where(v => v.GooglePlaceId == placeId).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(cancellationToken);
        var saves = venueId is { } vid
            ? await _db.Favorites.Where(f => f.VenueId == vid && f.CreatedAt >= since).Select(f => f.CreatedAt).ToListAsync(cancellationToken)
            : [];

        var perDay = Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new VenueStatsDay(
                d,
                rows.Where(r => r.Day == d && r.Name == "venue_view").Sum(r => r.Count),
                rows.Where(r => r.Day == d && r.Name == "directions").Sum(r => r.Count),
                saves.LongCount(s => s.Date == d)))
            .ToList();
        return new VenueStatsDto(
            perDay.Sum(d => d.Views),
            perDay.Sum(d => d.Directions),
            saves.Count,
            rows.Where(r => r.Name == "share").Sum(r => r.Count),
            perDay);
    }

    public async Task<VenueStatsDto> OwnerStatsAsync(string placeId, Guid ownerId, int days, CancellationToken cancellationToken = default)
    {
        var owner = await _db.Venues.Where(v => v.GooglePlaceId == placeId).Select(v => v.OwnerUserId).FirstOrDefaultAsync(cancellationToken);
        if (owner != ownerId)
        {
            throw new ForbiddenException("Bu mekanın istatistiklerini yalnızca sahibi görebilir.");
        }

        return await CountsAsync(placeId, days, cancellationToken);
    }

    public async Task<FunnelDto> FunnelAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = _clock.UtcNow.Date.AddDays(-Math.Clamp(days, 1, 90) + 1);
        var rows = await _db.DailyCounts
            .AsNoTracking()
            .Where(c => c.Day >= since)
            .GroupBy(c => new { c.Name, c.Key })
            .Select(g => new { g.Key.Name, g.Key.Key, Count = g.Sum(x => x.Count) })
            .ToListAsync(cancellationToken);
        var totals = rows.GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var intents = rows.Where(r => r.Name == AnalyticsRules.ListView)
            .OrderByDescending(r => r.Count).Select(r => new CountItem(r.Key, r.Count)).ToList();
        var venues = rows.Where(r => r.Name == "venue_view")
            .OrderByDescending(r => r.Count).Take(10).Select(r => new CountItem(r.Key, r.Count)).ToList();
        var others = totals.Where(t => !Funnel.Steps.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value);
        return new FunnelDto(Funnel.Compute(totals), intents, venues, others);
    }
}
