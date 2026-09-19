using AutoMapper;
using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Enums;
using Library.Core.Exceptions;
using Library.Core.Interfaces;

namespace Library.Service.Services;

public class MemberService(
    IMemberRepository members,
    ILoanRepository loans,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IMapper mapper) : IMemberService
{
    public async Task<PagedResult<MemberResponse>> GetAllAsync(PageQuery query, CancellationToken ct)
    {
        var (items, total) = await members.GetPagedAsync(query.SafePage, query.SafePageSize, ct);
        return new PagedResult<MemberResponse>(mapper.Map<List<MemberResponse>>(items), query.SafePage, query.SafePageSize, total);
    }

    public async Task<MemberResponse> GetByIdAsync(int id, CancellationToken ct)
    {
        var member = await members.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Member), id);
        return mapper.Map<MemberResponse>(member);
    }

    public async Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ValidationException("FullName is required.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ValidationException("A valid email is required.");
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        if (await members.GetByEmailAsync(request.Email, ct) is not null)
            throw new BusinessRuleException($"A member with email '{request.Email}' already exists.");

        var member = mapper.Map<Member>(request);
        member.PasswordHash = hasher.Hash(request.Password);
        member.Role = UserRole.Member; // Self-registration never grants Admin.

        await members.AddAsync(member, ct);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<MemberResponse>(member);
    }

    public async Task UpdateAsync(int id, UpdateMemberRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) throw new ValidationException("FullName is required.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            throw new ValidationException("A valid email is required.");

        var member = await members.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(nameof(Member), id);

        var existing = await members.GetByEmailAsync(request.Email, ct);
        if (existing is not null && existing.Id != id)
            throw new BusinessRuleException($"A member with email '{request.Email}' already exists.");

        mapper.Map(request, member);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var member = await members.GetForUpdateAsync(id, ct) ?? throw new NotFoundException(nameof(Member), id);
        if (await loans.MemberHasLoansAsync(id, ct))
            throw new BusinessRuleException("A member with loan history cannot be deleted.");

        members.Remove(member);
        await uow.SaveChangesAsync(ct);
    }
}
