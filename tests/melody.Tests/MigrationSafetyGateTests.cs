using melody.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace melody.Tests;

/// <summary>
/// Proves the shipped EF Core migration applies cleanly against a real SQL Server target and
/// produces the schema objects the application relies on (unique reservation-slot index, Identity
/// role-name index, and the Reservations foreign keys). Closes the gap where <c>Testing</c>-environment
/// runs bypass migrations entirely via <c>EnsureCreatedAsync()</c>.
/// </summary>
/// <remarks>
/// Reads its connection string from <c>MIGRATION_GATE_CONNECTION_STRING</c>; when that variable is
/// absent, the test no-ops so local <c>dotnet test</c> runs are unaffected without a local SQL Server
/// instance. CI always sets this against a fresh, empty database container.
/// </remarks>
public class MigrationSafetyGateTests
{
    private static ServiceProvider BuildServiceProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task MigrateAsync_AgainstRealSqlServer_AppliesCleanlyAndCreatesExpectedSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable("MIGRATION_GATE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var provider = BuildServiceProvider(connectionString);
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var exception = await Record.ExceptionAsync(() => db.Database.MigrateAsync());
        Assert.True(exception is null, $"Database.MigrateAsync() threw: {exception}");

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var reservationsUniqueIndexExists = await ScalarAsync<int>(connection, """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'IX_Reservations_RoomId_DayOfWeek_StartTime'
              AND object_id = OBJECT_ID('Reservations')
              AND is_unique = 1
            """);
        Assert.True(reservationsUniqueIndexExists == 1,
            "Expected unique index 'IX_Reservations_RoomId_DayOfWeek_StartTime' on table 'Reservations' was not found.");

        var roleNameIndexExists = await ScalarAsync<int>(connection, """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE name = 'RoleNameIndex'
              AND object_id = OBJECT_ID('AspNetRoles')
            """);
        Assert.True(roleNameIndexExists == 1,
            "Expected index 'RoleNameIndex' on table 'AspNetRoles' (NormalizedName) was not found.");

        var foreignKeys = new[]
        {
            "FK_Reservations_Rooms_RoomId",
            "FK_Reservations_Students_StudentId",
            "FK_Reservations_Teachers_TeacherId",
        };
        foreach (var foreignKeyName in foreignKeys)
        {
            var foreignKeyExists = await ScalarAsync<int>(connection, $"""
                SELECT COUNT(*)
                FROM sys.foreign_keys
                WHERE name = '{foreignKeyName}'
                """);
            Assert.True(foreignKeyExists == 1, $"Expected foreign key '{foreignKeyName}' was not found.");
        }
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }
}
