using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Models.Base;

namespace Wanes.DataAccess.UnitOfWorks;

/// <summary>
/// Coordinates repositories and the transaction boundary. Atomic multi-entity
/// operations (e.g. confirm booking + decrement seats) wrap Begin/Commit/RollBack.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;

    Task<int> SaveAsync();

    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollBackAsync();
}
