using LibraryCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryData.Configurations;

/// <summary>
/// Fluent API rules for <see cref="Loan"/> - the join entity between <see cref="User"/> and
/// <see cref="Book"/>. Both foreign keys are Restrict: you cannot delete a book or a member
/// that still has loan history.
/// </summary>
public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.BorrowedAtUtc)
               .IsRequired();

        builder.Property(l => l.DueAtUtc)
               .IsRequired();

        builder.Property(l => l.Status)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.HasOne(l => l.Book)
               .WithMany(b => b.Loans)
               .HasForeignKey(l => l.BookId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Member)
               .WithMany(u => u.Loans)
               .HasForeignKey(l => l.MemberId)
               .OnDelete(DeleteBehavior.Restrict);

        // Fast lookup of "the active loan for this book" - used by the borrow/return flow.
        builder.HasIndex(l => new { l.BookId, l.Status });
    }
}
