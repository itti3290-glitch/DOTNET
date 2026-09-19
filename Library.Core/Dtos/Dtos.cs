using Library.Core.Enums;

namespace Library.Core.Dtos;

// ---- Books ----
public record CreateBookRequest(string Title, string Author, string Isbn, int TotalCopies, IReadOnlyList<int>? CategoryIds);

public record UpdateBookRequest(string Title, string Author, string Isbn, int TotalCopies, IReadOnlyList<int>? CategoryIds);

public record BookResponse(
    int Id, string Title, string Author, string Isbn,
    int TotalCopies, int AvailableCopies, IReadOnlyList<string> Categories);

// ---- Members ----
public record CreateMemberRequest(string FullName, string Email, string Password);

public record UpdateMemberRequest(string FullName, string Email);

public record MemberResponse(int Id, string FullName, string Email, string Role);

// ---- Loans ----
public record CreateLoanRequest(int BookId, int MemberId);

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
public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, MemberResponse Member);

public record ReturnLoanRequest(int LoanId);
