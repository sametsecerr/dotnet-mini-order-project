using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Data;

namespace OrderApp.Tests;

/// <summary>
/// Her test için izole, in-memory SQLite veritabanı kurar.
/// InMemory provider yerine gerçek SQLite kullanıyoruz; çünkü transaction ve
/// check constraint davranışını da test etmek istiyoruz.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    /// <summary>Her çağrıda taze bir DbContext döner (change tracker paylaşılmaz).</summary>
    public AppDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
