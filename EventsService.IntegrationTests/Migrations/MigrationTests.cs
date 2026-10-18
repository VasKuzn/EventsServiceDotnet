using EventsService.Domain.Models;
using EventsService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventsService.IntegrationTests.Migrations;

public class MigrationTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private const string InitialMigrationId = "InitialCreate";

    private async Task<List<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> map, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }
        return rows;
    }

    private async Task<Dictionary<string, (string Type, bool Nullable, int? MaxLength)>> GetColumnsAsync(string table)
    {
        var rows = await QueryAsync(
            """
            SELECT column_name, data_type, is_nullable, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table
            """,
            r => (
                Name: r.GetString(0),
                Type: r.GetString(1),
                Nullable: r.GetString(2) == "YES",
                MaxLength: r.IsDBNull(3) ? (int?)null : r.GetInt32(3)),
            new NpgsqlParameter("table", table));

        return rows.ToDictionary(r => r.Name, r => (r.Type, r.Nullable, r.MaxLength));
    }

    [Fact]
    public async Task Migrate_OnEmptyDatabase_AppliesInitialMigrationWithoutPendingOnes()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();

        // Assert
        var migration = Assert.Single(applied);
        Assert.EndsWith(InitialMigrationId, migration);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Migrate_CalledTwice_IsIdempotent()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        await context.Database.MigrateAsync();

        // Assert
        Assert.Single(await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public void Migrations_MatchCurrentModel_NoPendingModelChanges()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var hasPendingChanges = context.Database.HasPendingModelChanges();

        // Assert
        Assert.False(hasPendingChanges, "Модель изменилась, но миграция для неё не создана.");
    }

    [Fact]
    public async Task Schema_ContainsEventsAndBookingsTables()
    {
        // Act
        var tables = await QueryAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'",
            r => r.GetString(0));

        // Assert
        Assert.Equivalent(new[] { "events", "bookings", "__EFMigrationsHistory" }, tables);
    }

    [Fact]
    public async Task Schema_EventsTable_HasExpectedColumnsTypesAndNullability()
    {
        // Act
        var columns = await GetColumnsAsync("events");

        // Assert
        Assert.Equal(
            new Dictionary<string, (string Type, bool Nullable, int? MaxLength)>
            {
                ["id"] = ("uuid", false, null),
                ["title"] = ("character varying", false, 250),
                ["description"] = ("character varying", true, 2500),
                ["start_at"] = ("timestamp with time zone", false, null),
                ["end_at"] = ("timestamp with time zone", false, null),
                ["total_seats"] = ("integer", false, null),
                ["available_seats"] = ("integer", false, null),
            },
            columns);
    }

    [Fact]
    public async Task Schema_BookingsTable_HasExpectedColumnsTypesAndNullability()
    {
        // Act
        var columns = await GetColumnsAsync("bookings");

        // Assert
        Assert.Equal(
            new Dictionary<string, (string Type, bool Nullable, int? MaxLength)>
            {
                ["id"] = ("uuid", false, null),
                ["event_id"] = ("uuid", false, null),
                ["status"] = ("character varying", false, 20),
                ["created_at"] = ("timestamp with time zone", false, null),
                ["processed_at"] = ("timestamp with time zone", true, null),
            },
            columns);
    }

    [Theory]
    [InlineData("events")]
    [InlineData("bookings")]
    public async Task Schema_Tables_HavePrimaryKeyOnId(string table)
    {
        // Act
        var primaryKeyColumns = await QueryAsync(
            """
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            WHERE tc.table_schema = 'public' AND tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'
            """,
            r => r.GetString(0),
            new NpgsqlParameter("table", table));

        // Assert
        Assert.Equal(["id"], primaryKeyColumns);
    }

    [Fact]
    public async Task Schema_Bookings_HasCascadingForeignKeyToEvents()
    {
        // Act
        var foreignKeys = await QueryAsync(
            """
            SELECT kcu.column_name, ccu.table_name, ccu.column_name, rc.delete_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
              ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
            JOIN information_schema.referential_constraints rc
              ON tc.constraint_name = rc.constraint_name AND tc.table_schema = rc.constraint_schema
            JOIN information_schema.constraint_column_usage ccu
              ON rc.unique_constraint_name = ccu.constraint_name AND rc.unique_constraint_schema = ccu.constraint_schema
            WHERE tc.table_schema = 'public' AND tc.table_name = 'bookings' AND tc.constraint_type = 'FOREIGN KEY'
            """,
            r => (Column: r.GetString(0), RefTable: r.GetString(1), RefColumn: r.GetString(2), DeleteRule: r.GetString(3)));

        // Assert
        var foreignKey = Assert.Single(foreignKeys);
        Assert.Equal("event_id", foreignKey.Column);
        Assert.Equal("events", foreignKey.RefTable);
        Assert.Equal("id", foreignKey.RefColumn);
        Assert.Equal("CASCADE", foreignKey.DeleteRule);
    }

    [Fact]
    public async Task Schema_Bookings_HasIndexOnEventId()
    {
        // Act
        var indexedColumns = await QueryAsync(
            """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_class t ON t.oid = i.indrelid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(i.indkey)
            WHERE t.relname = 'bookings' AND NOT i.indisprimary
            """,
            r => r.GetString(0));

        // Assert
        Assert.Equal(["event_id"], indexedColumns);
    }

    [Fact]
    public async Task Database_InsertBookingForUnknownEvent_ViolatesForeignKeyConstraint()
    {
        // Arrange
        await using var context = CreateContext();
        context.Add(Booking.Create(Guid.NewGuid()));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        // Assert
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
    }

    [Fact]
    public async Task ResetDatabase_AfterDataWasInserted_ReturnsEmptyDatabase()
    {
        // Arrange
        await SeedAsync(TestData.CreateEvent());

        // Act
        await Fixture.ResetDatabaseAsync();

        // Assert
        await using var context = CreateContext();
        Assert.Empty(await context.Events.ToListAsync());
    }
}
