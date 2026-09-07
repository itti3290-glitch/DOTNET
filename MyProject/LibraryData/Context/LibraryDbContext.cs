using LibraryCore.Entities;
using Microsoft.EntityFrameworkCore;

namespace LibraryData.Context;

/// <summary>
/// EF Core context for the library. Code-First: every <see cref="DbSet{TEntity}"/> becomes a
/// table, and the per-entity Fluent API rules live in <c>LibraryData/Configurations</c> and are
/// picked up by <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// The provider and connection string are supplied from the API layer via
/// <c>AddDbContext</c> - this class knows nothing about where the database is.
/// </summary>
public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
    }
}
