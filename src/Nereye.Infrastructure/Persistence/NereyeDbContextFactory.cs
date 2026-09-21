using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nereye.Infrastructure.Persistence;

public sealed class NereyeDbContextFactory : IDesignTimeDbContextFactory<NereyeDbContext>
{
    public NereyeDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<NereyeDbContext>()
            .UseNpgsql("Host=localhost;Database=nereye;Username=postgres;Password=postgres")
            .Options;
        return new NereyeDbContext(options);
    }
}
