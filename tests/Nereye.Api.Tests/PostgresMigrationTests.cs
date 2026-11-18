using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nereye.Domain.Entities;
using Nereye.Infrastructure.Persistence;
using Npgsql;

namespace Nereye.Api.Tests;

/// <summary>
/// Gerçek Postgres ister: NEREYE_TEST_PG = "Host=...;Port=...;Username=...;Password=..." (veritabanı adı olmadan).
/// CI bu değişkeni bir Postgres servisiyle verir; yoksa test "atlandı" görünür, geçmiş sayılmaz.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NEREYE_TEST_PG")))
        {
            Skip = "NEREYE_TEST_PG tanımlı değil (gerçek Postgres gerekir).";
        }
    }
}

public sealed class PostgresMigrationTests
{
    private const string BeforeTimestampTz = "20261118075201_RefreshTokenReuseDetection";

    [PostgresFact]
    public async Task TimestampTz_migration_keeps_stored_utc_values_and_accepts_unspecified_kinds()
    {
        var server = Environment.GetEnvironmentVariable("NEREYE_TEST_PG")!;
        var name = "nereye_mig_" + Guid.NewGuid().ToString("N")[..12];
        // Oturum saat dilimi bilerek UTC değil: dönüşüm buna rağmen kaymamalı.
        var cs = new NpgsqlConnectionStringBuilder(server) { Database = name, Timezone = "Europe/Istanbul" }.ConnectionString;
        await using (var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<NereyeDbContext>().UseNpgsql(cs).Options;
            var tokenId = Guid.NewGuid();
            await using (var db = new NereyeDbContext(options))
            {
                await db.GetService<IMigrator>().MigrateAsync(BeforeTimestampTz);
                Assert.Equal("timestamp without time zone", await ColumnType(db));
                await db.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "RefreshTokens" ("Id","UserId","TokenHash","ExpiresAt","CreatedAt","Revoked") VALUES ({0},{1},'h','2026-02-01 00:00:00','2026-01-02 03:04:05',false)""",
                    tokenId, Guid.NewGuid());
                await db.Database.MigrateAsync();
                Assert.Equal("timestamp with time zone", await ColumnType(db));
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }

            await using (var db = new NereyeDbContext(options))
            {
                var stored = await db.RefreshTokens.SingleAsync(t => t.Id == tokenId);
                Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
                Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), stored.CreatedAt);

                // Türü belirsiz bir değer UTC sayılır; Npgsql'in "Kind=Unspecified" hatasıyla yazma düşmez.
                var unspecified = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Unspecified);
                db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TokenHash = "x", CreatedAt = unspecified, ExpiresAt = unspecified.AddDays(1) });
                await db.SaveChangesAsync();
                Assert.True(await db.RefreshTokens.AnyAsync(t => t.CreatedAt == new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)));
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
            await admin.OpenAsync();
            await new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
        }
    }

    private static async Task<string> ColumnType(NereyeDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT data_type FROM information_schema.columns WHERE table_name = 'RefreshTokens' AND column_name = 'CreatedAt'";
            return (string)(await cmd.ExecuteScalarAsync())!;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
