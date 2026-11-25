using Nereye.Domain.Collections;

namespace Nereye.Domain.Tests;

public class CollectionRulesTests
{
    [Theory]
    [InlineData("Kahve rotam", "Kahve rotam")]
    [InlineData("  Hafta   sonu  ", "Hafta sonu")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Names_are_trimmed_and_collapsed(string input, string? expected) =>
        Assert.Equal(expected, CollectionRules.CleanName(input));

    [Fact]
    public void Name_longer_than_limit_is_rejected()
    {
        Assert.Null(CollectionRules.CleanName(new string('a', CollectionRules.NameMax + 1)));
        Assert.NotNull(CollectionRules.CleanName(new string('a', CollectionRules.NameMax)));
    }

    [Fact]
    public void Same_name_check_ignores_case_with_turkish_letters()
    {
        Assert.True(CollectionRules.SameName("İstanbul Kahveleri", "istanbul kahveleri"));
        Assert.True(CollectionRules.SameName("IŞIK", "ışık"));
        Assert.False(CollectionRules.SameName("Kahve", "Kahveler"));
    }

    [Fact]
    public void Limits()
    {
        Assert.True(CollectionRules.CanCreate(CollectionRules.MaxCollections - 1));
        Assert.False(CollectionRules.CanCreate(CollectionRules.MaxCollections));
        Assert.True(CollectionRules.CanAdd(CollectionRules.MaxItems - 1));
        Assert.False(CollectionRules.CanAdd(CollectionRules.MaxItems));
    }
}
