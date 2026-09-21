namespace Nereye.Application.Preferences;

public sealed record PreferenceDto(int DefaultRadiusMeters, IReadOnlyList<Intent> Intents, IReadOnlyList<VibeTag> Vibes);

public interface IPreferenceService
{
    Task<PreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PreferenceDto> UpsertAsync(Guid userId, PreferenceDto dto, CancellationToken cancellationToken = default);
}
