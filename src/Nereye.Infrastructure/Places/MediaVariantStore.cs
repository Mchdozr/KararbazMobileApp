using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Nereye.Infrastructure.Places;

/// <summary>
/// Yerel görsellerin küçültülmüş sürümlerini diskte tutar. Anahtar, kaynak yol + boyut + değişme zamanı +
/// genişliktir: kaynak değişince (yorum fotoğrafı silinip yeniden yüklenince) eski sürüm kullanılmaz.
/// Aynı sürüm için tek kodlama yapılır; eşzamanlı kodlama işlemci sayısıyla sınırlıdır.
/// </summary>
public sealed class MediaVariantStore
{
    private readonly string _root;
    private readonly ILogger<MediaVariantStore>? _log;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly SemaphoreSlim _encoders = new(Math.Max(1, Environment.ProcessorCount));
    private int _encodes;

    public MediaVariantStore(string root, ILogger<MediaVariantStore>? log = null)
    {
        _root = root;
        _log = log;
    }

    /// <summary>Bu süreçte yapılan kodlama sayısı (testler için).</summary>
    public int Encodes => Volatile.Read(ref _encodes);

    /// <summary>Sürümün disk yolu; kaynak zaten dar ya da okunamıyorsa null (özgün dosya sunulur).</summary>
    public async Task<string?> GetAsync(FileInfo source, int width, CancellationToken cancellationToken = default)
    {
        var key = Key(source, width);
        var path = Path.Combine(_root, width.ToString(System.Globalization.CultureInfo.InvariantCulture), key + source.Extension.ToLowerInvariant());
        var none = path + ".none";
        if (File.Exists(path))
        {
            return path;
        }

        if (File.Exists(none))
        {
            return null;
        }

        var gate = _gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                return path;
            }

            await _encoders.WaitAsync(cancellationToken);
            try
            {
                var input = await File.ReadAllBytesAsync(source.FullName, cancellationToken);
                var resized = ImageSanitizer.ResizeToWidth(input, width);
                Interlocked.Increment(ref _encodes);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (resized is not { } variant)
                {
                    await File.WriteAllBytesAsync(none, [], cancellationToken);
                    return null;
                }

                // Önce geçici dosyaya yaz: yarım dosya hiçbir zaman sunulmasın.
                var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(temp, variant.Bytes, cancellationToken);
                File.Move(temp, path, overwrite: true);
                return path;
            }
            finally
            {
                _encoders.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log?.LogWarning(ex, "Görsel sürümü üretilemedi: {File}", source.Name);
            return null;
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
            {
                _gates.TryRemove(new KeyValuePair<string, SemaphoreSlim>(path, gate));
            }
        }
    }

    private static string Key(FileInfo source, int width)
    {
        var text = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}|{width}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }
}
