namespace Nereye.Application.Venues;

public sealed record PlacePhotosRequest(IReadOnlyList<string>? PlaceIds = null);

public sealed record PlacePhotosResponse(IReadOnlyDictionary<string, string> Photos);
