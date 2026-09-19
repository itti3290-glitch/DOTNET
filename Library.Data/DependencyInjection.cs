using Library.Core.Interfaces;
using Library.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Data;

public static class DependencyInjection
{
    /// <summary>Registers the DbContext, repositories and unit of work. Called only from Program.cs.</summary>
    public static IServiceCollection AddDataLayer(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LibraryDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}
