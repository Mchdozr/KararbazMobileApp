using Nereye.Application.Common;

namespace Nereye.Infrastructure.Places;

/// <summary>Sunum anı: "açık mı" hesabı önbellekteki bayrak yerine bu anla yapılır.</summary>
internal static class ServeTime
{
    public static DateTimeOffset Now(IClock clock) => new(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc));
}
