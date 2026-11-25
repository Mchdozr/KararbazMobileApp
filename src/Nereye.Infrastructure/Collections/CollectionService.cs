using Microsoft.EntityFrameworkCore;
using Nereye.Application.Collections;
using Nereye.Application.Common;
using Nereye.Domain.Collections;
using Nereye.Domain.Entities;
using Nereye.Domain.Geo;
using Nereye.Domain.Polls;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Collections;

public sealed class CollectionService : ICollectionService
{
    private readonly NereyeDbContext _db;
    private readonly IClock _clock;

    public CollectionService(NereyeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CollectionSummaryDto>> ListAsync(Guid userId, string? placeId, CancellationToken cancellationToken = default)
    {
        var lists = await _db.VenueLists.AsNoTracking().Include(l => l.Items)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.UpdatedAt)
            .ToListAsync(cancellationToken);
        return lists.Select(l => new CollectionSummaryDto(
                l.Id,
                l.Name,
                l.Items.Count,
                l.Items.OrderByDescending(i => i.AddedAt).Select(i => i.PhotoUrl).FirstOrDefault(p => p is not null),
                l.ShareCode is not null,
                placeId is not null && l.Items.Any(i => i.PlaceId == placeId)))
            .ToList();
    }

    public async Task<CollectionDto> CreateAsync(Guid userId, string? name, CancellationToken cancellationToken = default)
    {
        var clean = CollectionRules.CleanName(name) ?? throw new ValidationException($"Liste adı 1–{CollectionRules.NameMax} karakter olmalı.");
        var names = await _db.VenueLists.Where(l => l.UserId == userId).Select(l => l.Name).ToListAsync(cancellationToken);
        if (!CollectionRules.CanCreate(names.Count))
        {
            throw new ValidationException($"En fazla {CollectionRules.MaxCollections} liste oluşturabilirsin.");
        }

        if (names.Any(n => CollectionRules.SameName(n, clean)))
        {
            throw new ValidationException("Bu adda bir listen zaten var.");
        }

        var now = _clock.UtcNow;
        var list = new VenueList { Id = Guid.NewGuid(), UserId = userId, Name = clean, CreatedAt = now, UpdatedAt = now };
        _db.VenueLists.Add(list);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(list, null);
    }

    public async Task<CollectionDto> RenameAsync(Guid userId, Guid id, string? name, string? webBase, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        var clean = CollectionRules.CleanName(name) ?? throw new ValidationException($"Liste adı 1–{CollectionRules.NameMax} karakter olmalı.");
        var others = await _db.VenueLists.Where(l => l.UserId == userId && l.Id != id).Select(l => l.Name).ToListAsync(cancellationToken);
        if (others.Any(n => CollectionRules.SameName(n, clean)))
        {
            throw new ValidationException("Bu adda bir listen zaten var.");
        }

        list.Name = clean;
        list.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(list, webBase);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        _db.VenueLists.Remove(list);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CollectionDto> GetAsync(Guid userId, Guid id, string? webBase, CancellationToken cancellationToken = default) =>
        ToDto(await OwnAsync(userId, id, cancellationToken), webBase);

    public async Task<CollectionDto> AddItemAsync(Guid userId, Guid id, AddItemRequest item, string? webBase, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        var placeId = PollRules.Clip(item.PlaceId, 128);
        var name = PollRules.Clip(item.Name, PollRules.NameMax);
        if (placeId is null || name is null || !GeoBounds.IsValid(item.Latitude, item.Longitude))
        {
            throw new ValidationException("Mekan bilgisi eksik.");
        }

        if (list.Items.Any(i => i.PlaceId == placeId))
        {
            return ToDto(list, webBase);
        }

        if (!CollectionRules.CanAdd(list.Items.Count))
        {
            throw new ValidationException($"Bir listede en fazla {CollectionRules.MaxItems} mekan olabilir.");
        }

        var now = _clock.UtcNow;
        var entry = new VenueListItem
        {
            Id = Guid.NewGuid(),
            ListId = list.Id,
            PlaceId = placeId,
            Name = name,
            Category = PollRules.Clip(item.Category, 60),
            PhotoUrl = PollRules.CleanPhotoUrl(item.PhotoUrl),
            Latitude = item.Latitude,
            Longitude = item.Longitude,
            AddedAt = now
        };
        _db.VenueListItems.Add(entry);
        if (!list.Items.Contains(entry))
        {
            list.Items.Add(entry);
        }

        list.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(list, webBase);
    }

    public async Task RemoveItemAsync(Guid userId, Guid id, string placeId, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        var entry = list.Items.FirstOrDefault(i => i.PlaceId == placeId);
        if (entry is null)
        {
            return;
        }

        _db.VenueListItems.Remove(entry);
        list.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShareDto> ShareAsync(Guid userId, Guid id, string? webBase, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        if (list.ShareCode is null)
        {
            for (var i = 0; i < 5 && list.ShareCode is null; i++)
            {
                var code = CollectionRules.NewShareCode();
                if (!await _db.VenueLists.AnyAsync(l => l.ShareCode == code, cancellationToken))
                {
                    list.ShareCode = code;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Share(list.ShareCode!, webBase);
    }

    public async Task UnshareAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var list = await OwnAsync(userId, id, cancellationToken);
        list.ShareCode = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicListDto?> PublicAsync(string code, CancellationToken cancellationToken = default)
    {
        if (!CollectionRules.IsShareCode(code))
        {
            return null;
        }

        var list = await _db.VenueLists.AsNoTracking().Include(l => l.Items).FirstOrDefaultAsync(l => l.ShareCode == code, cancellationToken);
        return list is null ? null : new PublicListDto(list.Name, Items(list), list.UpdatedAt);
    }

    private async Task<VenueList> OwnAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        // Başkasının listesi "yok" görünür: varlığı da sızmasın.
        await _db.VenueLists.Include(l => l.Items).FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId, cancellationToken)
        ?? throw new NotFoundException("Liste bulunamadı.");

    private static IReadOnlyList<CollectionItemDto> Items(VenueList list) =>
        list.Items.OrderByDescending(i => i.AddedAt)
            .Select(i => new CollectionItemDto(i.PlaceId, i.Name, i.Category, i.PhotoUrl, i.Latitude, i.Longitude, i.AddedAt))
            .ToList();

    private static ShareDto Share(string code, string? webBase) =>
        new(code, $"nereyego://list/{code}", webBase is null ? null : $"{webBase}/l/{code}");

    private static CollectionDto ToDto(VenueList list, string? webBase) =>
        new(list.Id, list.Name, Items(list), list.ShareCode is null ? null : Share(list.ShareCode, webBase));
}
