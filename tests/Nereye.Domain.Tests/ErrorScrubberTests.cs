using Nereye.Domain.Telemetry;

namespace Nereye.Domain.Tests;

public class ErrorScrubberTests
{
    [Fact]
    public void Removes_emails_tokens_coordinates_and_phone_numbers()
    {
        var raw = "Fetch failed for deniz.kaya@example.com at /api/recommendations?lat=40.98853&lng=29.02504 " +
                  "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.abcDEF123_- tel 05321234567 (40.9885, 29.0250)";
        var clean = ErrorScrubber.Scrub(raw);

        Assert.DoesNotContain("deniz.kaya", clean);
        Assert.DoesNotContain("eyJhbGci", clean);
        Assert.DoesNotContain("40.98853", clean);
        Assert.DoesNotContain("29.0250", clean);
        Assert.DoesNotContain("05321234567", clean);
        Assert.Contains("<e-posta>", clean);
        Assert.Contains("<anahtar>", clean);
        Assert.Contains("/api/recommendations?<sorgu>", clean);
        Assert.Contains("Fetch failed", clean);
    }

    [Fact]
    public void Keeps_ordinary_stack_frames_readable()
    {
        const string stack = "TypeError: undefined is not an object (evaluating 'card.name')\n    at VenueProductCard (index.bundle:1:23456)";
        Assert.Equal(stack, ErrorScrubber.Scrub(stack));
    }

    [Fact]
    public void Truncates_and_handles_null()
    {
        Assert.Equal("", ErrorScrubber.Scrub(null));
        Assert.Equal(20, ErrorScrubber.Scrub(new string('a', 100), 20).Length);
    }

    [Fact]
    public void Fingerprint_ignores_numbers_and_line_positions()
    {
        var a = ErrorFingerprint.Of("app", "Request 1234 failed", "at load (index.bundle:1:2345)\nat run (index.bundle:1:99)");
        var b = ErrorFingerprint.Of("app", "Request 98 failed", "at load (index.bundle:1:777)\nat run (index.bundle:1:3)");
        var c = ErrorFingerprint.Of("app", "Other failure", "at load (index.bundle:1:2345)");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.Equal(32, a.Length);
    }
}
