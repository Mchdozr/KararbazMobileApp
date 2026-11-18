using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nereye.Infrastructure.Persistence;

/// <summary>DateTime ↔ UTC. Belirsiz tür UTC sayılır (uygulama zamanı hep UtcNow ile üretir).</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => ToUtc(v),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
{
    public static DateTime ToUtc(DateTime v) => v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc)
    };
}
