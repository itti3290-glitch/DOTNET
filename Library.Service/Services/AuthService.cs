using AutoMapper;
using Library.Core.Dtos;
using Library.Core.Exceptions;
using Library.Core.Interfaces;

namespace Library.Service.Services;

public class AuthService(IMemberRepository members, IPasswordHasher hasher, IMapper mapper) : IAuthService
{
    public async Task<MemberResponse> ValidateCredentialsAsync(LoginRequest request, CancellationToken ct)
    {
        var member = string.IsNullOrWhiteSpace(request.Email) ? null : await members.GetByEmailAsync(request.Email, ct);

        // Same error for "no such user" and "wrong password" so accounts can't be probed.
        if (member is null || !hasher.Verify(request.Password, member.PasswordHash))
            throw new AuthenticationFailedException();

        return mapper.Map<MemberResponse>(member);
    }
}
