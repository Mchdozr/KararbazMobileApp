namespace Nereye.Application.Polls;

public sealed record PollOptionInput(string? PlaceId, string? Name, string? Category, string? PhotoUrl, double Latitude, double Longitude);

public sealed record CreatePollRequest(string? Title, IReadOnlyList<PollOptionInput>? Options);

public sealed record VoteRequest(string? VoterKey, string? PlaceId);

public sealed record PollOptionDto(string PlaceId, string Name, string? Category, string? PhotoUrl, double Latitude, double Longitude, int Votes);

public sealed record PollDto(
    string Code,
    string Title,
    DateTime ExpiresAt,
    bool Expired,
    IReadOnlyList<PollOptionDto> Options,
    int TotalVotes,
    string? MyVote,
    string? WinnerPlaceId,
    string AppUrl,
    string? WebUrl);

public interface IPollService
{
    Task<PollDto> CreateAsync(CreatePollRequest request, string? webBase, CancellationToken cancellationToken = default);

    Task<PollDto?> GetAsync(string code, string? voterKey, string? webBase, CancellationToken cancellationToken = default);

    Task<PollDto> VoteAsync(string code, VoteRequest request, string? webBase, CancellationToken cancellationToken = default);
}
