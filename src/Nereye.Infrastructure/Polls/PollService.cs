using Microsoft.EntityFrameworkCore;
using Nereye.Application.Common;
using Nereye.Application.Polls;
using Nereye.Domain.Entities;
using Nereye.Domain.Geo;
using Nereye.Domain.Polls;
using Nereye.Infrastructure.Persistence;

namespace Nereye.Infrastructure.Polls;

public sealed class PollService : IPollService
{
    private readonly NereyeDbContext _db;
    private readonly IClock _clock;

    public PollService(NereyeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PollDto> CreateAsync(CreatePollRequest request, string? webBase, CancellationToken cancellationToken = default)
    {
        var inputs = request.Options ?? [];
        if (!PollRules.OptionCountOk(inputs.Count))
        {
            throw new ValidationException($"Oylama için {PollRules.MinOptions}–{PollRules.MaxOptions} mekan seç.");
        }

        var options = new List<GroupPollOption>();
        foreach (var (o, i) in inputs.Select((o, i) => (o, i)))
        {
            var placeId = PollRules.Clip(o.PlaceId, 128);
            var name = PollRules.Clip(o.Name, PollRules.NameMax);
            if (placeId is null || name is null || !GeoBounds.IsValid(o.Latitude, o.Longitude))
            {
                throw new ValidationException("Mekan bilgisi eksik.");
            }

            if (options.Any(x => x.PlaceId == placeId))
            {
                throw new ValidationException("Aynı mekan iki kez eklenemez.");
            }

            options.Add(new GroupPollOption
            {
                Id = Guid.NewGuid(),
                Position = i,
                PlaceId = placeId,
                Name = name,
                Category = PollRules.Clip(o.Category, 60),
                PhotoUrl = PollRules.CleanPhotoUrl(o.PhotoUrl),
                Latitude = o.Latitude,
                Longitude = o.Longitude
            });
        }

        var now = _clock.UtcNow;
        var poll = new GroupPoll
        {
            Id = Guid.NewGuid(),
            Code = await FreeCodeAsync(cancellationToken),
            Title = PollRules.Clip(request.Title, PollRules.TitleMax) ?? "Nereye gidelim?",
            CreatedAt = now,
            ExpiresAt = now + PollRules.Lifetime,
            Options = options
        };
        _db.GroupPolls.Add(poll);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(poll, null, webBase);
    }

    public async Task<PollDto?> GetAsync(string code, string? voterKey, string? webBase, CancellationToken cancellationToken = default)
    {
        var poll = await LoadAsync(code, cancellationToken);
        return poll is null ? null : ToDto(poll, voterKey, webBase);
    }

    public async Task<PollDto> VoteAsync(string code, VoteRequest request, string? webBase, CancellationToken cancellationToken = default)
    {
        if (!PollRules.IsVoterKey(request.VoterKey))
        {
            throw new ValidationException("Geçersiz oy.");
        }

        var poll = await LoadAsync(code, cancellationToken) ?? throw new NotFoundException("Oylama bulunamadı.");
        if (_clock.UtcNow >= poll.ExpiresAt)
        {
            throw new ValidationException("Oylama sona erdi.");
        }

        if (poll.Options.All(o => o.PlaceId != request.PlaceId))
        {
            throw new ValidationException("Bu mekan oylamada yok.");
        }

        var hash = PollRules.HashVoter(request.VoterKey!);
        var now = _clock.UtcNow;
        var vote = poll.Votes.FirstOrDefault(v => v.VoterHash == hash);
        if (vote is null)
        {
            vote = new GroupPollVote { Id = Guid.NewGuid(), PollId = poll.Id, VoterHash = hash, PlaceId = request.PlaceId!, CreatedAt = now, UpdatedAt = now };
            _db.GroupPollVotes.Add(vote);
            // EF eklenen oyu izlenen oylamanın koleksiyonuna kendisi bağlar; bağlamadıysa elle (çift sayım olmasın).
            if (!poll.Votes.Contains(vote))
            {
                poll.Votes.Add(vote);
            }
        }
        else
        {
            vote.PlaceId = request.PlaceId!;
            vote.UpdatedAt = now;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Aynı cihaz aynı anda iki kez bastı: tekil dizin ikinciyi reddeder, ilki geçerli.
            _db.ChangeTracker.Clear();
            poll = await LoadAsync(code, cancellationToken) ?? throw new NotFoundException("Oylama bulunamadı.");
        }

        return ToDto(poll, request.VoterKey, webBase);
    }

    private Task<GroupPoll?> LoadAsync(string code, CancellationToken cancellationToken) =>
        !PollRules.IsCode(code)
            ? Task.FromResult<GroupPoll?>(null)
            : _db.GroupPolls.Include(p => p.Options).Include(p => p.Votes).AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);

    private async Task<string> FreeCodeAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 5; i++)
        {
            var code = PollRules.NewCode();
            if (!await _db.GroupPolls.AnyAsync(p => p.Code == code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Oylama kodu üretilemedi.");
    }

    private PollDto ToDto(GroupPoll poll, string? voterKey, string? webBase)
    {
        var counts = poll.Votes.GroupBy(v => v.PlaceId).ToDictionary(g => g.Key, g => g.Count());
        var options = poll.Options.OrderBy(o => o.Position)
            .Select(o => new PollOptionDto(o.PlaceId, o.Name, o.Category, o.PhotoUrl, o.Latitude, o.Longitude, counts.GetValueOrDefault(o.PlaceId)))
            .ToList();
        var hash = PollRules.IsVoterKey(voterKey) ? PollRules.HashVoter(voterKey!) : null;
        var mine = hash is null ? null : poll.Votes.FirstOrDefault(v => v.VoterHash == hash)?.PlaceId;
        var winner = PollRules.Winner(poll.Options.Select(o => (o.PlaceId, o.Position, counts.GetValueOrDefault(o.PlaceId))));
        return new PollDto(
            poll.Code,
            poll.Title,
            poll.ExpiresAt,
            _clock.UtcNow >= poll.ExpiresAt,
            options,
            poll.Votes.Count,
            mine,
            winner,
            $"nereyego://poll/{poll.Code}",
            webBase is null ? null : $"{webBase}/k/{poll.Code}");
    }
}
