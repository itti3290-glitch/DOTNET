using System.ComponentModel.DataAnnotations;
using Library.Core.Enums;

namespace Library.Core.Dtos;

// ---- Books ----
public record CreateBookRequest(
    [Required, StringLength(300)] string Title,
    [Required, StringLength(200)] string Author,
    [Required, StringLength(20, MinimumLength = 10)] string Isbn,
    [Range(0, 1000)] int TotalCopies,
    IReadOnlyList<int>? CategoryIds);

public record UpdateBookRequest(
    [Required, StringLength(300)] string Title,
    [Required, StringLength(200)] string Author,
    [Required, StringLength(20, MinimumLength = 10)] string Isbn,
    [Range(0, 1000)] int TotalCopies,
    IReadOnlyList<int>? CategoryIds);

public record BookResponse(
    int Id, string Title, string Author, string Isbn,
    int TotalCopies, int AvailableCopies, IReadOnlyList<string> Categories);

// ---- Members ----
public record CreateMemberRequest(
    [Required, StringLength(200)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record UpdateMemberRequest(
    [Required, StringLength(200)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email);

public record MemberResponse(int Id, string FullName, string Email, string Role);

// ---- Loans ----
public record CreateLoanRequest(
    [Range(1, int.MaxValue)] int BookId,
    [Range(1, int.MaxValue)] int MemberId);

public record LoanResponse(
    int Id, int BookId, string BookTitle, int MemberId, string MemberName,
    DateTime LoanedAt, DateTime? ReturnedAt, LoanStatus Status);

// ---- Paging ----
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record PageQuery(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize < 1 ? 20 : Math.Min(PageSize, MaxPageSize);
}

// ---- Auth ----
public record LoginRequest(
    [Required, StringLength(256)] string Email,
    [Required, StringLength(100)] string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, MemberResponse Member);


/// <summary>Book list filters and sorting. Every filter is optional; all run inside the SQL query.</summary>
public record BookQuery(
    [StringLength(100)] string? Search = null,
    [StringLength(100)] string? Author = null,
    int? CategoryId = null,
    bool AvailableOnly = false,
    BookSortBy SortBy = BookSortBy.Title,
    bool Desc = false,
    int Page = 1,
    int PageSize = 20) : PageQuery(Page, PageSize);
