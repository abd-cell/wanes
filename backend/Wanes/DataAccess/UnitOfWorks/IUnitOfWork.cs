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

    /// <summary>
    /// Forgets every tracked entity, so the next read comes from the database
    /// rather than from the identity map.
    ///
    /// Needed to retry an operation that lost a row-version race: the entity
    /// that failed to save is still tracked, still holds the stale version, and
    /// re-reading it would hand back the same in-memory instance — so a retry
    /// without this would fail forever on the same stale token.
    /// </summary>
    void Detach();
}
