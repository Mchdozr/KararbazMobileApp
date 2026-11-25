namespace Nereye.Domain.Entities;

/// <summary>Arkadaşlarla karar: kısa liste oylaması. Hesap gerektirmez; 24 saat açık kalır.</summary>
public sealed class GroupPoll
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<GroupPollOption> Options { get; set; } = [];
    public List<GroupPollVote> Votes { get; set; } = [];
}

public sealed class GroupPollOption
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public int Position { get; set; }
    public required string PlaceId { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public string? PhotoUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>Oy: oy verenin cihazdaki rastgele anahtarının özeti. Kişiye ait başka alan yok.</summary>
public sealed class GroupPollVote
{
    public Guid Id { get; set; }
    public Guid PollId { get; set; }
    public required string VoterHash { get; set; }
    public required string PlaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
