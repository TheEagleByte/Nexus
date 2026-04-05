using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nexus.Hub.Domain.Entities;
using Nexus.Hub.Infrastructure.Data;

namespace Nexus.Hub.Api.Tests.Helpers;

/// <summary>
/// Creates SQLite in-memory NexusDbContext instances for integration testing.
/// SQLite provides real SQL semantics (constraints, FK, transactions) while
/// avoiding the need for a full PostgreSQL instance.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public NexusDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new TestNexusDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Derived DbContext that adds ValueConverters for JsonDocument columns
    /// so SQLite can store them as TEXT instead of requiring PostgreSQL jsonb.
    /// </summary>
    private sealed class TestNexusDbContext(DbContextOptions<NexusDbContext> options)
        : NexusDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // SQLite doesn't support DateTimeOffset natively — store as ISO 8601 TEXT
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToStringConverter>();
            configurationBuilder.Properties<DateTimeOffset?>()
                .HaveConversion<DateTimeOffsetToStringConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQLite doesn't support jsonb — store JsonDocument as TEXT
            var jsonConverter = new ValueConverter<JsonDocument, string>(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()));

            var nullableJsonConverter = new ValueConverter<JsonDocument?, string?>(
                v => v != null ? v.RootElement.GetRawText() : null,
                v => v != null ? JsonDocument.Parse(v, new JsonDocumentOptions()) : null);

            modelBuilder.Entity<Spoke>(entity =>
            {
                entity.Property(s => s.Capabilities).HasConversion(jsonConverter).HasColumnType("TEXT");
                entity.Property(s => s.Config).HasConversion(jsonConverter).HasColumnType("TEXT");
                entity.Property(s => s.Profile).HasConversion(nullableJsonConverter).HasColumnType("TEXT");
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.Property(a => a.Details).HasConversion(nullableJsonConverter).HasColumnType("TEXT");
            });
        }
    }
}
