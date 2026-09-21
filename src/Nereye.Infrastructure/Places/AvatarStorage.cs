using Microsoft.AspNetCore.Hosting;
using Nereye.Domain.Places;

namespace Nereye.Infrastructure.Places;

public sealed class AvatarStorage
{
    private readonly IWebHostEnvironment _env;

    public AvatarStorage(IWebHostEnvironment env) => _env = env;

    public string Folder => Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "media", "avatars");

    public string Save(Guid userId, byte[] bytes)
    {
        if (!AvatarFile.TryDetect(bytes, out var ext))
        {
            throw new InvalidOperationException("Sadece jpg, png veya webp.");
        }

        Directory.CreateDirectory(Folder);
        foreach (var old in Directory.GetFiles(Folder, userId.ToString("N") + ".*"))
        {
            File.Delete(old);
        }

        var name = userId.ToString("N") + ext;
        File.WriteAllBytes(Path.Combine(Folder, name), bytes);
        return "/media/avatars/" + name;
    }

    public string SaveVenue(byte[] bytes)
    {
        if (!AvatarFile.TryDetect(bytes, out var ext))
        {
            throw new InvalidOperationException("Sadece jpg, png veya webp.");
        }

        var folder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "media", "uploads");
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid().ToString("N") + ext;
        File.WriteAllBytes(Path.Combine(folder, name), bytes);
        return "/media/uploads/" + name;
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
