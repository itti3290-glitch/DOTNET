using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Data;

/// <summary>
/// Lets `dotnet ef` create the context without starting the API.
/// Set LIBRARY_CONNECTION to override the default LocalDB connection.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("LIBRARY_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=LibraryApiDb;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<LibraryDbContext>().UseSqlServer(connection).Options;
        return new LibraryDbContext(options);
    }
}
