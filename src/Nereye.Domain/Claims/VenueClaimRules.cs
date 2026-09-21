using Nereye.Domain.Entities;

namespace Nereye.Domain.Claims;

public static class VenueClaimRules
{
    public static bool CanSubmit(bool alreadyClaimed, bool hasPending) =>
        !alreadyClaimed && !hasPending;

    public static bool CanReject(ClaimStatus status) => status == ClaimStatus.Pending;
}
