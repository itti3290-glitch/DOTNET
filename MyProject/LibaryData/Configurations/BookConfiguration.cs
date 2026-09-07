using LibaryCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibaryData.Configurations;

/// <summary>Fluent API rules for <see cref="Book"/> - the limited resource.</summary>
public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(b => b.Author)
               .IsRequired()
               .HasMaxLength(120);

        builder.Property(b => b.Isbn)
               .IsRequired()
               .HasMaxLength(13);

        builder.HasIndex(b => b.Isbn)
               .IsUnique();

        builder.Property(b => b.Description)
               .HasMaxLength(2000);

        builder.Property(b => b.Status)
               .HasConversion<string>()
               .HasMaxLength(20);

        // Optimistic-concurrency token. Redundant with [Timestamp] on the entity, kept here so
        // the intent is visible in the model configuration too.
        builder.Property(b => b.RowVersion)
               .IsRowVersion();

        builder.HasMany(b => b.Loans)
               .WithOne(l => l.Book)
               .HasForeignKey(l => l.BookId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
