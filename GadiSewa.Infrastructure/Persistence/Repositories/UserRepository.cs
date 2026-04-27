using GadiSewa.Application.Interfaces.Persistence;
using GadiSewa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GadiSewa.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(GadiSewaDbContext dbContext) : base(dbContext)
    {
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return DbSet.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }
}
