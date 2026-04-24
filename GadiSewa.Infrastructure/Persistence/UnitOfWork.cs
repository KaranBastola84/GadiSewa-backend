using GadiSewa.Application.Interfaces.Persistence;

namespace GadiSewa.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly GadiSewaDbContext _dbContext;

    public UnitOfWork(GadiSewaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
