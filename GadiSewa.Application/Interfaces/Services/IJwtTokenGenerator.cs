using GadiSewa.Domain.Entities;

namespace GadiSewa.Application.Interfaces.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}
