using LibaryCore.Services;
using LibaryService.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LibaryService;

/// <summary>Registers the business-logic services. Repositories/DbContext come from <c>AddLibaryData</c>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLibaryServices(
        this IServiceCollection services, Action<LibaryOptions>? configure = null)
    {
        var options = new LibaryOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<ILoanService, LoanService>();

        return services;
    }
}
