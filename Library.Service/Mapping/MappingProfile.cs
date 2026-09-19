using AutoMapper;
using Library.Core.Dtos;
using Library.Core.Entities;
using Library.Core.Enums;

namespace Library.Service.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Books. Copy counts, RowVersion and categories are set by BookService, not mapped from requests.
        CreateMap<Book, BookResponse>()
            .ForCtorParam(nameof(BookResponse.Categories), o => o.MapFrom(b => b.Categories.Select(c => c.Name).OrderBy(n => n).ToList()));
        CreateMap<CreateBookRequest, Book>()
            .ForMember(b => b.Id, o => o.Ignore())
            .ForMember(b => b.AvailableCopies, o => o.Ignore())
            .ForMember(b => b.RowVersion, o => o.Ignore())
            .ForMember(b => b.Loans, o => o.Ignore())
            .ForMember(b => b.Categories, o => o.Ignore());
        CreateMap<UpdateBookRequest, Book>()
            .ForMember(b => b.Id, o => o.Ignore())
            .ForMember(b => b.TotalCopies, o => o.Ignore())
            .ForMember(b => b.AvailableCopies, o => o.Ignore())
            .ForMember(b => b.RowVersion, o => o.Ignore())
            .ForMember(b => b.Loans, o => o.Ignore())
            .ForMember(b => b.Categories, o => o.Ignore());

        // Members. The password hash and role are set by MemberService.
        CreateMap<Member, MemberResponse>()
            .ForCtorParam(nameof(MemberResponse.Role), o => o.MapFrom(m => m.Role.ToString()));
        CreateMap<CreateMemberRequest, Member>()
            .ForMember(m => m.Id, o => o.Ignore())
            .ForMember(m => m.PasswordHash, o => o.Ignore())
            .ForMember(m => m.Role, o => o.Ignore())
            .ForMember(m => m.Loans, o => o.Ignore());
        CreateMap<UpdateMemberRequest, Member>()
            .ForMember(m => m.Id, o => o.Ignore())
            .ForMember(m => m.PasswordHash, o => o.Ignore())
            .ForMember(m => m.Role, o => o.Ignore())
            .ForMember(m => m.Loans, o => o.Ignore());

        // Loans.
        CreateMap<Loan, LoanResponse>()
            .ForCtorParam(nameof(LoanResponse.BookTitle), o => o.MapFrom(l => l.Book.Title))
            .ForCtorParam(nameof(LoanResponse.MemberName), o => o.MapFrom(l => l.Member.FullName))
            .ForCtorParam(nameof(LoanResponse.Status), o => o.MapFrom(l => l.ReturnedAt == null ? LoanStatus.Active : LoanStatus.Returned));
        CreateMap<CreateLoanRequest, Loan>()
            .ForMember(l => l.Id, o => o.Ignore())
            .ForMember(l => l.Book, o => o.Ignore())
            .ForMember(l => l.Member, o => o.Ignore())
            .ForMember(l => l.LoanedAt, o => o.Ignore())
            .ForMember(l => l.ReturnedAt, o => o.Ignore());

        // Category is exposed only as a name inside BookResponse; this map keeps the entity mappable if needed.
        CreateMap<Category, string>().ConvertUsing(c => c.Name);
    }
}
