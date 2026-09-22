using Microsoft.EntityFrameworkCore;
using Nereye.Application.Preferences;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Preferences;

public sealed class PreferenceService : IPreferenceService
{
    private readonly NereyeDbContext _db;

    public PreferenceService(NereyeDbContext db) => _db = db;

    public async Task<PreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pref = await _db.UserPreferences
            .Include(p => p.Intents)
            .Include(p => p.Vibes)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (pref is null)
        {
            return new PreferenceDto(1500, [], []);
        }

        return new PreferenceDto(
            pref.DefaultRadiusMeters,
            pref.Intents.Select(i => i.Intent).ToList(),
            pref.Vibes.Select(v => v.Tag).ToList());
    }

    public async Task<PreferenceDto> UpsertAsync(Guid userId, PreferenceDto dto, CancellationToken cancellationToken = default)
    {
        var pref = await _db.UserPreferences
            .Include(p => p.Intents)
            .Include(p => p.Vibes)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (pref is null)
        {
            pref = new UserPreference { UserId = userId };
            _db.UserPreferences.Add(pref);
        }

        pref.DefaultRadiusMeters = SearchRadius.Clamp(dto.DefaultRadiusMeters);
        pref.Intents.Clear();
        foreach (var intent in dto.Intents.Distinct())
        {
            pref.Intents.Add(new UserPreferenceIntent { UserId = userId, Intent = intent });
        }

        pref.Vibes.Clear();
        foreach (var vibe in dto.Vibes.Distinct())
        {
            pref.Vibes.Add(new UserPreferenceVibe { UserId = userId, Tag = vibe });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }
}
