using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Data;

/// <summary>
/// Lets `dotnet ef` create the context without starting the API. Scaffolding a migration only
/// needs the provider, not a reachable server, so the default points at a local PostgreSQL;
/// set LIBRARY_CONNECTION to override it.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LIBRARY_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=LibraryApiDb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<LibraryDbContext>().UseNpgsql(connection).Options;
        return new LibraryDbContext(options);
    }
}
