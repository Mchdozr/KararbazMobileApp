using Microsoft.AspNetCore.Hosting;
using Nereye.Application.Common;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class AvatarStorage
{
    private readonly IWebHostEnvironment _env;

    public AvatarStorage(IWebHostEnvironment env) => _env = env;

    public string Folder => Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "media", "avatars");

    /// <summary>Profil fotoğrafı: en fazla 512 px, EXIF'siz yeniden kodlanır.</summary>
    public string Save(Guid userId, byte[] bytes)
    {
        if (!AvatarFile.TryDetect(bytes, out _))
        {
            throw new ValidationException("Sadece jpg, png veya webp.");
        }

        (bytes, var ext) = ImageSanitizer.Normalize(bytes, ImageSanitizer.AvatarMaxSide);

        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.GetFiles(Folder, userId.ToString("N") + ".*"))
        {
            File.Delete(old);
        }

        var name = userId.ToString("N") + ext;
        File.WriteAllBytes(Path.Combine(Folder, name), bytes);
        return "/media/avatars/" + name;
    }

    /// <summary>Yorum ve işletme fotoğrafı: en fazla 1600 px, EXIF (GPS dahil) atılarak yeniden kodlanır.</summary>
    public string SaveVenue(byte[] bytes)
    {
        if (!AvatarFile.TryDetect(bytes, out _))
        {
            throw new ValidationException("Sadece jpg, png veya webp.");
        }

        (bytes, var ext) = ImageSanitizer.Normalize(bytes, ImageSanitizer.UploadMaxSide);

        var folder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "media", "uploads");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + ext;
        File.WriteAllBytes(Path.Combine(folder, name), bytes);
        return "/media/uploads/" + name;
    }

    /// <summary>
    /// Yükleme dosyasını siler. Yalnızca bu sınıfın ürettiği "/media/uploads/{32 hex}.{uzantı}" yolları kabul
    /// edilir; dış adres ya da "../" içeren yol hiçbir dosyaya dokunmaz.
    /// </summary>
    public bool DeleteUpload(string? url)
    {
        if (!Domain.Reviews.ReviewRules.IsOwnUpload(url))
        {
            return false;
        }

        var name = Path.GetFileName(url!);
        var path = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "media", "uploads", name);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public void Delete(Guid userId)
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }

        foreach (var old in Directory.GetFiles(Folder, userId.ToString("N") + ".*"))
        {
            File.Delete(old);
        }
    }
}
