using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Entities;

namespace LibaryCore.Services;

/// <summary>Hashes and verifies passwords. The plain password never leaves this boundary.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Mints the signed bearer token handed back on register/login.</summary>
public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(User user);
}

/// <summary>Registration, login and "who am I".</summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserResponse> GetProfileAsync(int userId, CancellationToken ct = default);
}

/// <summary>Catalog management (manager) and browsing (anyone).</summary>
public interface IBookService
{
    Task<PagedResult<BookResponse>> GetBooksAsync(BookQuery query, CancellationToken ct = default);
    Task<BookResponse> GetBookAsync(int id, CancellationToken ct = default);
    Task<BookResponse> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default);
    Task<BookResponse> UpdateBookAsync(int id, UpdateBookRequest request, CancellationToken ct = default);
    Task DeleteBookAsync(int id, CancellationToken ct = default);
}

/// <summary>The borrow/return flow and the loan listings.</summary>
public interface ILoanService
{
    /// <summary>Borrow a book for a member. Resolves the race for the one physical copy.</summary>
    Task<LoanResponse> BorrowAsync(int bookId, int memberId, CancellationToken ct = default);

    /// <summary>
    /// Return a loaned book. A member may return their own loans; a manager may return anyone's.
    /// </summary>
    Task<LoanResponse> ReturnAsync(int loanId, int actingUserId, bool actingUserIsManager, CancellationToken ct = default);

    /// <summary>The calling member's own loan history.</summary>
    Task<PagedResult<LoanResponse>> GetMyLoansAsync(int memberId, LoanQuery query, CancellationToken ct = default);

    /// <summary>Every loan in the system. Manager only.</summary>
    Task<PagedResult<LoanResponse>> GetAllLoansAsync(LoanQuery query, CancellationToken ct = default);
}
