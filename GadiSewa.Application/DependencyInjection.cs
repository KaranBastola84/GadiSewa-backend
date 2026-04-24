using GadiSewa.Application.Interfaces.Services;
using GadiSewa.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GadiSewa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}
