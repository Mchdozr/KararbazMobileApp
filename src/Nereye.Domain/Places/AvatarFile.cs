namespace Nereye.Domain.Places;

public static class AvatarFile
{
    public const int MaxBytes = 2_000_000;

    public static bool TryDetect(ReadOnlySpan<byte> bytes, out string extension)
    {
        extension = "";
        if (bytes.Length < 12 || bytes.Length > MaxBytes)
        {
            return false;
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            extension = ".jpg";
            return true;
        }

        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            extension = ".png";
            return true;
        }

        if (bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[8] == (byte)'W' && bytes[9] == (byte)'E')
        {
            extension = ".webp";
            return true;
        }

        return false;
    }

    public static bool TryDecodeBase64(string? raw, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var s = raw.Trim();
        var comma = s.IndexOf(',');
        if (comma >= 0)
        {
            s = s[(comma + 1)..];
        }

        try
        {
            bytes = Convert.FromBase64String(s);
            return bytes.Length is > 11 and <= MaxBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
