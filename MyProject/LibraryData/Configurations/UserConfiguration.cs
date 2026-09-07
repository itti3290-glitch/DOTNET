using LibraryCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryData.Configurations;

/// <summary>Fluent API rules for <see cref="User"/> (borrower or manager).</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserName)
               .IsRequired()
               .HasMaxLength(50);

        builder.HasIndex(u => u.UserName)
               .IsUnique();

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(200);

        builder.HasIndex(u => u.Email)
               .IsUnique();

        builder.Property(u => u.PasswordHash)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(u => u.FullName)
               .IsRequired()
               .HasMaxLength(120);

        builder.Property(u => u.Role)
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.HasMany(u => u.Loans)
               .WithOne(l => l.Member)
               .HasForeignKey(l => l.MemberId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
