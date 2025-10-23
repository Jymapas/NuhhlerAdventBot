using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class DbSmokeTests
{
    [Fact]
    public void CanCreateInMemorySqliteSchema()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated().Should().BeTrue();
    }
}
