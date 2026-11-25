using Nereye.Domain.Polls;

namespace Nereye.Domain.Tests;

public class PollRulesTests
{
    [Fact]
    public void Code_is_eight_unambiguous_characters()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = PollRules.NewCode();
            Assert.Equal(8, code.Length);
            Assert.True(PollRules.IsCode(code), code);
            Assert.DoesNotMatch("[01ilo]", code);
        }

        Assert.False(PollRules.IsCode("ABCDEFGH"));
        Assert.False(PollRules.IsCode("abc"));
        Assert.False(PollRules.IsCode("../../x1"));
    }

    [Theory]
    [InlineData("a1b2c3d4e5f6g7h8", true)]
    [InlineData("short", false)]
    [InlineData("has space in the key!!", false)]
    [InlineData(null, false)]
    public void Voter_key_shape(string? key, bool ok) => Assert.Equal(ok, PollRules.IsVoterKey(key));

    [Fact]
    public void Voter_key_is_stored_only_as_a_hash()
    {
        var hash = PollRules.HashVoter("a1b2c3d4e5f6g7h8");
        Assert.Equal(32, hash.Length);
        Assert.DoesNotContain("a1b2c3d4", hash);
        Assert.Equal(hash, PollRules.HashVoter("a1b2c3d4e5f6g7h8"));
    }

    [Fact]
    public void Winner_has_most_votes_and_ties_go_to_the_earlier_option()
    {
        Assert.Null(PollRules.Winner([("a", 0, 0), ("b", 1, 0)]));
        Assert.Equal("b", PollRules.Winner([("a", 0, 1), ("b", 1, 3), ("c", 2, 2)]));
        Assert.Equal("a", PollRules.Winner([("b", 1, 2), ("a", 0, 2)]));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Two_to_five_options(int count, bool ok) => Assert.Equal(ok, PollRules.OptionCountOk(count));

    [Fact]
    public void Photo_urls_are_local_paths_or_https_only()
    {
        Assert.Equal("/media/venues/petra.jpg", PollRules.CleanPhotoUrl("/media/venues/petra.jpg"));
        Assert.Equal("https://upload.wikimedia.org/a.jpg", PollRules.CleanPhotoUrl("https://upload.wikimedia.org/a.jpg"));
        Assert.Null(PollRules.CleanPhotoUrl("javascript:alert(1)"));
        Assert.Null(PollRules.CleanPhotoUrl("http://insecure/a.jpg"));
        Assert.Null(PollRules.CleanPhotoUrl("//evil.example/a.jpg"));
    }
}
