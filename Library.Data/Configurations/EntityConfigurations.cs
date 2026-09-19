using Library.Core.Entities;
using Library.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(300);
        b.Property(x => x.Author).IsRequired().HasMaxLength(200);
        b.Property(x => x.Isbn).IsRequired().HasMaxLength(20);
        b.HasIndex(x => x.Isbn).IsUnique();

        // Optimistic concurrency: SQL Server rowversion, checked on every UPDATE/DELETE.
        b.Property(x => x.RowVersion).IsRowVersion();

        // Invariants enforced by the database as a last line of defence.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Books_AvailableCopies_NonNegative", "[AvailableCopies] >= 0");
            t.HasCheckConstraint("CK_Books_AvailableCopies_LteTotal", "[AvailableCopies] <= [TotalCopies]");
        });

        // Many-to-many: Book <-> Category (EF creates the BookCategory join table).
        b.HasMany(x => x.Categories)
            .WithMany(c => c.Books)
            .UsingEntity(j => j.ToTable("BookCategory"));

        b.HasData(
            new Book { Id = 1, Title = "Clean Code", Author = "Robert C. Martin", Isbn = "9780132350884", TotalCopies = 3, AvailableCopies = 3 },
            new Book { Id = 2, Title = "The Pragmatic Programmer", Author = "Andrew Hunt, David Thomas", Isbn = "9780135957059", TotalCopies = 2, AvailableCopies = 2 },
            new Book { Id = 3, Title = "Dune", Author = "Frank Herbert", Isbn = "9780441013593", TotalCopies = 1, AvailableCopies = 1 });
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Name).IsUnique();

        b.HasData(
            new Category { Id = 1, Name = "Programming" },
            new Category { Id = 2, Name = "Science Fiction" },
            new Category { Id = 3, Name = "Non-Fiction" });
    }
}

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

        // DEV/DEMO ONLY: admin password "Admin123!", member (Dana) password "Member123!". PBKDF2 hashes in the same format as PasswordHasher. Change or remove before any real deployment.
        b.HasData(
            new Member { Id = 1, FullName = "Library Admin", Email = "admin@library.local", PasswordHash = "v1.210000.IRzm9BJmjQ3lBW+ICEoajQ==.a5+PbzmF1az6vrnPDwyIjjW93HlY+881dxC+xz7oQaQ=", Role = UserRole.Admin },
            new Member { Id = 2, FullName = "Dana Levi", Email = "dana@library.local", PasswordHash = "v1.210000.gk+khtvbDLcxJWq9rolcZw==.bl/tr0AX88InBEEjMRfj4r13yydmhusBwAuS99t3+oU=", Role = UserRole.Member });
    }
}

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> b)
    {
        b.HasKey(x => x.Id);

        // One-to-many: Book -> Loans, Member -> Loans. Restrict: history must not vanish with its parent.
        b.HasOne(x => x.Book).WithMany(x => x.Loans).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Member).WithMany(x => x.Loans).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);

        // A member cannot hold two open loans of the same book.
        b.HasIndex(x => new { x.BookId, x.MemberId })
            .IsUnique()
            .HasFilter("[ReturnedAt] IS NULL")
            .HasDatabaseName("UX_Loans_ActivePerBookMember");
    }
}
