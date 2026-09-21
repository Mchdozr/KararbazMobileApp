namespace Nereye.Domain.Entities;

public sealed class ConsentLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Purpose { get; set; }
    public bool Granted { get; set; }
    public DateTime CreatedAt { get; set; }
}
