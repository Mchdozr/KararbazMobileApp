using Nereye.Domain.Media;

namespace Nereye.Domain.Tests;

public class ImageVariantsTests
{
    [Theory]
    [InlineData(1, 400)]
    [InlineData(400, 400)]
    [InlineData(401, 800)]
    [InlineData(528, 800)]
    [InlineData(1179, 1200)]
    [InlineData(1200, 1200)]
    public void Requested_width_snaps_up_to_a_known_variant(int requested, int expected)
    {
        Assert.Equal(expected, ImageVariants.Snap(requested));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1201)]
    [InlineData(5000)]
    public void Out_of_range_width_means_original(int requested)
    {
        Assert.Null(ImageVariants.Snap(requested));
    }

    [Theory]
    [InlineData("/media/venues/petra.jpg", true)]
    [InlineData("/media/uploads/2026/11/a1b2.webp", true)]
    [InlineData("/media/uploads/x.PNG", true)]
    [InlineData("/media/avatars/u.jpg", false)]
    [InlineData("/media/venues/ATTRIBUTION.md", false)]
    [InlineData("/api/media/google", false)]
    [InlineData("/media/venues/../secret.jpg", false)]
    public void Only_venue_and_upload_images_have_variants(string path, bool expected)
    {
        Assert.Equal(expected, ImageVariants.Supports(path));
    }
}
