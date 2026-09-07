using LibaryCore.DTOs.Responses;
using LibaryCore.Entities;
using LibaryCore.Enums;

namespace LibaryService.Services;

/// <summary>Entity -> response-DTO projections, kept in one place.</summary>
internal static class Mapping
{
    public static UserResponse ToResponse(this User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.ToString()
    };

    public static BookResponse ToResponse(this Book book) => new()
    {
        Id = book.Id,
        Title = book.Title,
        Author = book.Author,
        Isbn = book.Isbn,
        PublicationYear = book.PublicationYear,
        Description = book.Description,
        Status = book.Status.ToString(),
        CreatedAtUtc = book.CreatedAtUtc
    };

    public static LoanResponse ToResponse(this Loan loan, DateTime utcNow) => new()
    {
        Id = loan.Id,
        BookId = loan.BookId,
        BookTitle = loan.Book?.Title ?? string.Empty,
        MemberId = loan.MemberId,
        MemberName = loan.Member?.FullName ?? string.Empty,
        BorrowedAtUtc = loan.BorrowedAtUtc,
        DueAtUtc = loan.DueAtUtc,
        ReturnedAtUtc = loan.ReturnedAtUtc,
        Status = loan.Status.ToString(),
        IsOverdue = loan.Status == LoanStatus.Active && loan.DueAtUtc < utcNow
    };
}
