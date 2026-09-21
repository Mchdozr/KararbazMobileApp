using Nereye.Domain;
using Nereye.Domain.Recommendation;

namespace Nereye.Domain.Tests;

public class VibeMatchTests
{
    [Fact]
    public void Empty_user_vibes_is_neutral_half()
    {
        Assert.Equal(0.5, VibeMatcher.Score([], [VibeTag.Sessiz]), 6);
    }

    [Fact]
    public void Full_overlap_is_one()
    {
        var user = new[] { VibeTag.Laptop, VibeTag.Sessiz };
        Assert.Equal(1, VibeMatcher.Score(user, user), 6);
    }

    [Fact]
    public void Half_overlap_is_half()
    {
        var user = new[] { VibeTag.Laptop, VibeTag.Sessiz };
        var venue = new[] { VibeTag.Laptop };
        Assert.Equal(0.5, VibeMatcher.Score(user, venue), 6);
    }
}
