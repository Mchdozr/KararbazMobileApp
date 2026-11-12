using Nereye.Domain.Accounts;
using Nereye.Domain.Geo;
using Nereye.Domain.Reviews;

namespace Nereye.Domain.Tests;

public sealed class AccountRulesTests
{
    [Theory]
    [InlineData("  Ada@Example.COM ", "ada@example.com")]
    [InlineData("a.b+tag@nereye.app", "a.b+tag@nereye.app")]
    public void Email_is_trimmed_and_lowercased(string raw, string expected) =>
        Assert.Equal(expected, AccountRules.NormalizeEmail(raw));

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    [InlineData("Ada <ada@example.com>")]
    public void Bad_email_is_rejected(string raw) => Assert.Null(AccountRules.NormalizeEmail(raw));

    [Fact]
    public void Password_needs_length_letter_and_digit()
    {
        Assert.NotNull(AccountRules.PasswordProblem("kisa1"));
        Assert.NotNull(AccountRules.PasswordProblem("sadeceharf"));
        Assert.NotNull(AccountRules.PasswordProblem("1234567890"));
        Assert.Null(AccountRules.PasswordProblem("kahve1234"));
    }

    [Fact]
    public void Display_name_collapses_whitespace_and_caps_length()
    {
        Assert.Equal("Ada Lovelace", AccountRules.CleanDisplayName("  Ada   Lovelace "));
        Assert.Null(AccountRules.CleanDisplayName("   "));
        Assert.Equal(AccountRules.MaxDisplayNameLength, AccountRules.CleanDisplayName(new string('a', 90))!.Length);
    }

    [Fact]
    public void Review_photos_must_be_own_uploads()
    {
        Assert.True(ReviewRules.IsOwnUpload("/media/uploads/0123456789abcdef0123456789abcdef.jpg"));
        Assert.False(ReviewRules.IsOwnUpload("https://evil.example/x.jpg"));
        Assert.False(ReviewRules.IsOwnUpload("/media/uploads/../avatars/x.jpg"));
        Assert.False(ReviewRules.IsOwnUpload("/media/uploads/0123456789abcdef0123456789abcdef.svg"));
    }

    [Fact]
    public void Review_text_is_trimmed_and_capped()
    {
        Assert.Null(ReviewRules.CleanText("   "));
        Assert.Equal("iyi", ReviewRules.CleanText(" iyi "));
        Assert.Equal(ReviewRules.MaxTextLength, ReviewRules.CleanText(new string('x', 5000))!.Length);
    }

    [Theory]
    [InlineData(40.98, 29.02, true)]
    [InlineData(0, 0, false)]
    [InlineData(91, 29, false)]
    [InlineData(40, 181, false)]
    [InlineData(double.NaN, 29, false)]
    public void Geo_bounds(double lat, double lng, bool ok) => Assert.Equal(ok, GeoBounds.IsValid(lat, lng));
}
