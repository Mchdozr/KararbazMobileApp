using Nereye.Domain.Claims;
using Nereye.Domain.Entities;

namespace Nereye.Domain.Tests;

public class VenueClaimRulesTests
{
    [Fact]
    public void Cannot_claim_already_claimed_venue()
    {
        Assert.False(VenueClaimRules.CanSubmit(alreadyClaimed: true, hasPending: false));
    }

    [Fact]
    public void Cannot_submit_second_pending_claim()
    {
        Assert.False(VenueClaimRules.CanSubmit(alreadyClaimed: false, hasPending: true));
    }

    [Fact]
    public void Open_venue_without_pending_can_be_claimed()
    {
        Assert.True(VenueClaimRules.CanSubmit(alreadyClaimed: false, hasPending: false));
    }

    [Fact]
    public void Only_pending_claim_can_be_rejected()
    {
        Assert.True(VenueClaimRules.CanReject(ClaimStatus.Pending));
        Assert.False(VenueClaimRules.CanReject(ClaimStatus.Approved));
        Assert.False(VenueClaimRules.CanReject(ClaimStatus.Rejected));
    }
}
