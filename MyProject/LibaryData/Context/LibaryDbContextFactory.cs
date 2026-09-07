using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LibaryData.Context;

/// <summary>
/// Used only by the EF Core command-line tools (<c>dotnet ef migrations add</c>, <c>database update</c>).
/// It lets the tools build a context without booting the whole Web API. The connection string here
/// is a design-time placeholder - it is never used at runtime, where the real one comes from
/// configuration via <c>AddLibaryData</c>. Override it with the <c>LIBARY_DESIGN_CONNECTION</c>
/// environment variable if you want the tools to hit a specific server.
/// </summary>
public sealed class LibaryDbContextFactory : IDesignTimeDbContextFactory<LibaryDbContext>
{
    public LibaryDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("LIBARY_DESIGN_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=LibaryDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        var options = new DbContextOptionsBuilder<LibaryDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(LibaryDbContext).Assembly.FullName))
            .Options;

        return new LibaryDbContext(options);
    }
}
