using Microsoft.EntityFrameworkCore;
using Nereye.Application.Places;
using Nereye.Domain.Places;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Places;

public sealed class SavedPlaceService : ISavedPlaceService
{
    private readonly NereyeDbContext _db;
    private readonly Application.Common.IClock _clock;

    public SavedPlaceService(NereyeDbContext db, Application.Common.IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SavedPlaceDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.SavedPlaces
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Kind == "home" ? 0 : p.Kind == "work" ? 1 : 2)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<SavedPlaceDto> AddAsync(Guid userId, UpsertSavedPlaceRequest request, CancellationToken cancellationToken = default)
    {
        if (!SavedPlaceRules.ValidCoord(request.Latitude, request.Longitude))
        {
            throw new InvalidOperationException("Konum geçersiz.");
        }

        var count = await _db.SavedPlaces.CountAsync(p => p.UserId == userId, cancellationToken);
        if (!SavedPlaceRules.CanAdd(count))
        {
            throw new InvalidOperationException("En fazla 20 kayıtlı konum.");
        }

        var kind = SavedPlaceRules.NormalizeKind(request.Kind);
        if (kind is "home" or "work")
        {
            var existing = await _db.SavedPlaces.FirstOrDefaultAsync(
                p => p.UserId == userId && p.Kind == kind,
                cancellationToken);
            if (existing is not null)
            {
                existing.Label = SavedPlaceRules.NormalizeLabel(request.Label, kind);
                existing.Latitude = request.Latitude;
                existing.Longitude = request.Longitude;
                await _db.SaveChangesAsync(cancellationToken);
                return Map(existing);
            }
        }

        var row = new SavedPlace
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Label = SavedPlaceRules.NormalizeLabel(request.Label, kind),
            Kind = kind,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CreatedAt = _clock.UtcNow
        };
        _db.SavedPlaces.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    public async Task RemoveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.SavedPlaces.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
        if (row is null)
        {
            return;
        }

        _db.SavedPlaces.Remove(row);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static SavedPlaceDto Map(SavedPlace p) => new(p.Id, p.Label, p.Kind, p.Latitude, p.Longitude);
}
