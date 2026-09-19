using Library.Core.Dtos;

namespace Library.Core.Interfaces;

public interface IBookService
{
    Task<PagedResult<BookResponse>> GetAllAsync(BookQuery query, CancellationToken ct);
    Task<BookResponse> GetByIdAsync(int id, CancellationToken ct);
    Task<BookResponse> CreateAsync(CreateBookRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateBookRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public interface IMemberService
{
    Task<PagedResult<MemberResponse>> GetAllAsync(PageQuery query, CancellationToken ct);
    Task<MemberResponse> GetByIdAsync(int id, CancellationToken ct);
    Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateMemberRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public interface ILoanService
{
    Task<PagedResult<LoanResponse>> GetAllAsync(int? memberId, bool activeOnly, PageQuery query, CancellationToken ct);
    Task<LoanResponse> GetByIdAsync(int id, CancellationToken ct);
    /// <summary>Loans a copy. Throws NoCopiesAvailable / ConcurrencyConflict / NotFound / BusinessRule.</summary>
    Task<LoanResponse> LoanBookAsync(CreateLoanRequest request, CancellationToken ct);
    Task<LoanResponse> ReturnBookAsync(int loanId, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IAuthService
{
    /// <summary>Returns the member for valid credentials; throws AuthenticationFailedException otherwise.</summary>
    Task<MemberResponse> ValidateCredentialsAsync(LoginRequest request, CancellationToken ct);
}
