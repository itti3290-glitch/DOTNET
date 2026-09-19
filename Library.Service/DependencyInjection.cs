using Library.Core.Interfaces;
using Library.Service.Mapping;
using Library.Service.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Service;

public static class DependencyInjection
{
    /// <summary>Registers services, the password hasher and AutoMapper profiles.</summary>
    public static IServiceCollection AddServiceLayer(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(MappingProfile).Assembly));
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
