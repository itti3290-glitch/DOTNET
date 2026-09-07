using LibaryCore.Entities;
using LibaryCore.Enums;
using LibaryCore.Services;
using LibaryData.Context;
using Microsoft.EntityFrameworkCore;

namespace LibaryData.Seed;

/// <summary>
/// Puts the database into a usable state on first run: applies pending migrations, creates one
/// manager account (there is no self-service way to become a manager), and drops in a few books
/// so the catalog is not empty. Safe to run on every startup - each step is a no-op once done.
/// </summary>
public static class DbSeeder
{
    public const string ManagerUserName = "manager";
    public const string ManagerEmail = "manager@Libary.local";
    public const string ManagerDefaultPassword = "Manager123!";

    public static async Task SeedAsync(
        LibaryDbContext context, IPasswordHasher passwordHasher, CancellationToken ct = default)
    {
        await context.Database.MigrateAsync(ct);

        var now = DateTime.UtcNow;

        if (!await context.Users.AnyAsync(u => u.Role == UserRole.Manager, ct))
        {
            context.Users.Add(new User
            {
                UserName = ManagerUserName,
                Email = ManagerEmail,
                FullName = "Library Manager",
                PasswordHash = passwordHasher.Hash(ManagerDefaultPassword),
                Role = UserRole.Manager,
                CreatedAtUtc = now
            });
        }

        if (!await context.Books.AnyAsync(ct))
        {
            context.Books.AddRange(
                NewBook("Clean Code", "Robert C. Martin", "9780132350884", 2008, now),
                NewBook("The Pragmatic Programmer", "Andrew Hunt, David Thomas", "9780201616224", 1999, now),
                NewBook("Design Patterns", "Erich Gamma et al.", "9780201633610", 1994, now),
                NewBook("Refactoring", "Martin Fowler", "9780134757599", 2018, now),
                NewBook("Domain-Driven Design", "Eric Evans", "9780321125217", 2003, now));
        }

        await context.SaveChangesAsync(ct);
    }

    private static Book NewBook(string title, string author, string isbn, int year, DateTime now) => new()
    {
        Title = title,
        Author = author,
        Isbn = isbn,
        PublicationYear = year,
        Status = BookStatus.Available,
        CreatedAtUtc = now
    };
}
