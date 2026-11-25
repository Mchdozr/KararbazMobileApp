using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nereye.Application.Common;
using Nereye.Application.Places;
using Nereye.Application.Push;
using Nereye.Domain.Entities;
using Nereye.Domain.Places;
using Nereye.Domain.Push;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Push;

/// <summary>Bildirim ayarları (<c>Push</c>). Kapalıyken hiçbir şey gönderilmez; erişim anahtarı isteğe bağlıdır (sır).</summary>
public sealed class PushOptions
{
    public const string Section = "Push";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://exp.host/--/api/v2/push/send";
    public string? AccessToken { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>Expo push servisi: 100'lük paketler; "DeviceNotRegistered" dönen belirteç geçersiz sayılır.</summary>
public sealed class ExpoPushSender : IPushSender
{
    private readonly HttpClient _http;
    private readonly PushOptions _options;
    private readonly ILogger<ExpoPushSender> _log;

    public ExpoPushSender(HttpClient http, IOptions<PushOptions> options, ILogger<ExpoPushSender> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken = default)
    {
        var results = new List<PushResult>();
        foreach (var chunk in messages.Chunk(100))
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = JsonContent.Create(chunk.Select(m => new
                {
                    to = m.To,
                    title = m.Title,
                    body = m.Body,
                    sound = "default",
                    data = new { url = m.Url }
                }))
            };
            if (!string.IsNullOrWhiteSpace(_options.AccessToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            }

            try
            {
                using var res = await _http.SendAsync(req, cancellationToken);
                var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var data = json.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d.EnumerateArray().ToList() : [];
                for (var i = 0; i < chunk.Length; i++)
                {
                    var item = i < data.Count ? data[i] : default;
                    var ok = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("status", out var s) && s.GetString() == "ok";
                    var unregistered = item.ValueKind == JsonValueKind.Object
                                       && item.TryGetProperty("details", out var det)
                                       && det.TryGetProperty("error", out var err)
                                       && err.GetString() == "DeviceNotRegistered";
                    results.Add(new PushResult(chunk[i].To, ok, unregistered));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Bildirim paketi gönderilemedi");
                results.AddRange(chunk.Select(m => new PushResult(m.To, false, false)));
            }
        }

        return results;
    }
}

/// <summary>Bildirim kapalıyken: hiçbir şey gönderilmez.</summary>
public sealed class DisabledPushSender : IPushSender
{
    public Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PushResult>>(messages.Select(m => new PushResult(m.To, false, false)).ToList());
}

/// <summary>
/// "Kaydettiğin mekanda bu akşam etkinlik var" hatırlatması. Etkinliğe 30 dk – 3 sa kala, etkinlik ve cihaz başına
/// bir kez, cihaz başına günde en fazla 3 bildirim. Geçersiz belirteçler silinir.
/// </summary>
public sealed class EventReminderJob
{
    private readonly NereyeDbContext _db;
    private readonly IPushSender _sender;
    private readonly IClock _clock;
    private readonly IPlacesClient _places;
    private readonly IPlacesCache _cache;

    public EventReminderJob(NereyeDbContext db, IPushSender sender, IClock clock, IPlacesClient places, IPlacesCache cache)
    {
        _db = db;
        _sender = sender;
        _clock = clock;
        _places = places;
        _cache = cache;
    }

    /// <summary>Bir tur. Gönderilmeye çalışılan bildirim sayısını döner.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var from = now + PushRules.RemindFrom;
        var until = now + PushRules.RemindUntil;
        var events = await _db.CultureEvents.AsNoTracking().Include(e => e.Venue)
            .Where(e => e.StartsAt >= from && e.StartsAt <= until)
            .ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            return 0;
        }

        var dayStart = now.Date;
        var messages = new List<(PushMessage Message, Guid DeviceId, string Key)>();
        var perDevice = new Dictionary<Guid, int>();
        foreach (var e in events.Where(e => e.Venue is not null && PushRules.ShouldRemind(e.StartsAt, now)))
        {
            var venueId = e.VenueId;
            var fans = await _db.Favorites.Where(f => f.VenueId == venueId).Select(f => f.UserId).ToListAsync(cancellationToken);
            if (fans.Count == 0)
            {
                continue;
            }

            var devices = await _db.PushDevices.Where(d => fans.Contains(d.UserId)).ToListAsync(cancellationToken);
            var key = $"event:{e.Id}";
            var name = e.Venue!.OwnerDisplayName ?? await VenueNameAsync(e.Venue.GooglePlaceId, cancellationToken) ?? "Kaydettiğin mekan";
            var (title, body) = PushRules.EventReminder(e.Title, name, e.StartsAt);
            foreach (var device in devices)
            {
                if (await _db.PushLogs.AnyAsync(l => l.DeviceId == device.Id && l.Key == key, cancellationToken))
                {
                    continue;
                }

                if (!perDevice.TryGetValue(device.Id, out var today))
                {
                    today = await _db.PushLogs.CountAsync(l => l.DeviceId == device.Id && l.SentAt >= dayStart, cancellationToken);
                }

                if (!PushRules.UnderDailyCap(today))
                {
                    continue;
                }

                perDevice[device.Id] = today + 1;
                messages.Add((new PushMessage(device.Token, title, body, $"nereyego://venue/{e.Venue.GooglePlaceId}"), device.Id, key));
            }
        }

        if (messages.Count == 0)
        {
            return 0;
        }

        var results = await _sender.SendAsync(messages.Select(m => m.Message).ToList(), cancellationToken);
        var byToken = results.GroupBy(r => r.To).ToDictionary(g => g.Key, g => g.First());
        var deadTokens = results.Where(r => r.Unregistered).Select(r => r.To).ToHashSet();
        foreach (var (message, deviceId, key) in messages.Where(m => !deadTokens.Contains(m.Message.To)))
        {
            // Gönderilemeyen (servis hatası) bir sonraki turda yeniden denenir; başarılı olan kaydedilir.
            if (byToken.TryGetValue(message.To, out var r) && r.Ok)
            {
                _db.PushLogs.Add(new PushLog { Id = Guid.NewGuid(), DeviceId = deviceId, Key = key, SentAt = now });
            }
        }

        if (deadTokens.Count > 0)
        {
            _db.PushDevices.RemoveRange(await _db.PushDevices.Where(d => deadTokens.Contains(d.Token)).ToListAsync(cancellationToken));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    private async Task<string?> VenueNameAsync(string placeId, CancellationToken cancellationToken) =>
        (await _cache.TryGetAsync<PlaceSnapshot>(PlacesCacheKey.Details(placeId), cancellationToken)
         ?? await _places.DetailsAsync(placeId, cancellationToken))?.Name;
}

/// <summary>Hatırlatma işini aralıklarla çalıştırır (yalnızca Push:Enabled iken kayıtlı).</summary>
public sealed class EventReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PushOptions _options;
    private readonly ILogger<EventReminderService> _log;

    public EventReminderService(IServiceScopeFactory scopes, IOptions<PushOptions> options, ILogger<EventReminderService> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Interval);
        try
        {
            do
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var sent = await scope.ServiceProvider.GetRequiredService<EventReminderJob>().RunOnceAsync(stoppingToken);
                    if (sent > 0)
                    {
                        _log.LogInformation("Etkinlik hatırlatması: {Count} bildirim", sent);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogWarning(ex, "Etkinlik hatırlatması turu başarısız");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
