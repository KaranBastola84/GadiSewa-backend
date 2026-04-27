using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Infrastructure.Authentication;
using GadiSewa.Infrastructure.Communication;
using GadiSewa.Infrastructure.Persistence;
using GadiSewa.Infrastructure.Persistence.Repositories;
using GadiSewa.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GadiSewa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is missing in configuration.");
        }

        services.AddDbContext<GadiSewaDbContext>(options => options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
