namespace Nereye.Domain.Entities;

public sealed class UserPreference
{
    public Guid UserId { get; set; }
    public int DefaultRadiusMeters { get; set; } = 1500;
    public ICollection<UserPreferenceIntent> Intents { get; set; } = new List<UserPreferenceIntent>();
    public ICollection<UserPreferenceVibe> Vibes { get; set; } = new List<UserPreferenceVibe>();
}

public sealed class UserPreferenceIntent
{
    public Guid UserId { get; set; }
    public Intent Intent { get; set; }
}

public sealed class UserPreferenceVibe
{
    public Guid UserId { get; set; }
    public VibeTag Tag { get; set; }
}
