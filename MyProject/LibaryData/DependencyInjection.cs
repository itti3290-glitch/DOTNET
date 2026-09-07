using LibaryData.Context;
using LibaryData.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibaryData;

/// <summary>Registers the EF Core context and the repository/unit-of-work layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLibaryData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LibaryDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(LibaryDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();

        return services;
    }
}
