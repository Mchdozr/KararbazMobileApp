namespace Nereye.Application.Claims;

public sealed record ClaimRequest(string Message);

public sealed record ClaimDto(Guid Id, string PlaceId, ClaimStatus Status, string Message, DateTime CreatedAt);

public interface IClaimService
{
    Task<ClaimDto> SubmitAsync(string placeId, Guid userId, ClaimRequest request, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid claimId, CancellationToken cancellationToken = default);
}
