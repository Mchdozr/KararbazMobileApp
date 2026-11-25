namespace Nereye.Application.Collections;

public sealed record NameRequest(string? Name);

public sealed record AddItemRequest(string? PlaceId, string? Name, string? Category, string? PhotoUrl, double Latitude, double Longitude);

public sealed record CollectionSummaryDto(Guid Id, string Name, int Count, string? CoverUrl, bool Shared, bool Contains);

public sealed record CollectionItemDto(string PlaceId, string Name, string? Category, string? PhotoUrl, double Latitude, double Longitude, DateTime AddedAt);

public sealed record CollectionDto(Guid Id, string Name, IReadOnlyList<CollectionItemDto> Items, ShareDto? Share);

public sealed record ShareDto(string Code, string AppUrl, string? WebUrl);

/// <summary>Bağlantıyla açılan liste: sahibinin kimliği yok.</summary>
public sealed record PublicListDto(string Name, IReadOnlyList<CollectionItemDto> Items, DateTime UpdatedAt);

public interface ICollectionService
{
    Task<IReadOnlyList<CollectionSummaryDto>> ListAsync(Guid userId, string? placeId, CancellationToken cancellationToken = default);
    Task<CollectionDto> CreateAsync(Guid userId, string? name, CancellationToken cancellationToken = default);
    Task<CollectionDto> RenameAsync(Guid userId, Guid id, string? name, string? webBase, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<CollectionDto> GetAsync(Guid userId, Guid id, string? webBase, CancellationToken cancellationToken = default);
    Task<CollectionDto> AddItemAsync(Guid userId, Guid id, AddItemRequest item, string? webBase, CancellationToken cancellationToken = default);
    Task RemoveItemAsync(Guid userId, Guid id, string placeId, CancellationToken cancellationToken = default);
    Task<ShareDto> ShareAsync(Guid userId, Guid id, string? webBase, CancellationToken cancellationToken = default);
    Task UnshareAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<PublicListDto?> PublicAsync(string code, CancellationToken cancellationToken = default);
}
