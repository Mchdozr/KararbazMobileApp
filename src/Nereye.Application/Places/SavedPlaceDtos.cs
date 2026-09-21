namespace Nereye.Application.Places;

public sealed record SavedPlaceDto(Guid Id, string Label, string Kind, double Latitude, double Longitude);

public sealed record UpsertSavedPlaceRequest(string? Label, string? Kind, double Latitude, double Longitude);

public sealed record AvatarUploadRequest(string Base64, string? ContentType);

public interface ISavedPlaceService
{
    Task<IReadOnlyList<SavedPlaceDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SavedPlaceDto> AddAsync(Guid userId, UpsertSavedPlaceRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
