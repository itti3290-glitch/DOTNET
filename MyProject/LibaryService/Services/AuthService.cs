using LibaryCore.DTOs.Requests;
using LibaryCore.DTOs.Responses;
using LibaryCore.Entities;
using LibaryCore.Enums;
using LibaryCore.Exceptions;
using LibaryCore.Services;
using LibaryData.Repositories;

namespace LibaryService.Services;

/// <inheritdoc cref="IAuthService"/>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokenService;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokenService)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var userName = request.UserName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.GetByUserNameAsync(userName, ct) is not null)
            throw new ConflictException("That username is already taken.");

        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("That email is already registered.");

        var user = new User
        {
            UserName = userName,
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Borrower,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _users.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByUserNameOrEmailAsync(request.UserNameOrEmail.Trim(), ct);

        // Same error whether the account is missing or the password is wrong - do not leak which.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationException("Invalid username or password.");

        return BuildAuthResponse(user);
    }

    public async Task<UserResponse> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User", userId);

        return user.ToResponse();
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (token, expiresAtUtc) = _tokenService.CreateToken(user);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAtUtc = expiresAtUtc,
            User = user.ToResponse()
        };
    }
}
