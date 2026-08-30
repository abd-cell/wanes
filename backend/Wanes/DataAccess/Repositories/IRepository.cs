using System.Linq.Expressions;
using Wanes.Shareds.Models.Base;

namespace Wanes.DataAccess.Repositories;

/// <summary>
/// Generic data access. Services depend on this — never on the DbContext directly.
/// Soft-delete is not auto-filtered; pass <c>includeDeleted:false</c> (default) to exclude.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query(bool includeDeleted = false);

    /// <summary>Filtered query with an optional include shaper, e.g. <c>q =&gt; q.Include(x =&gt; x.Trip)</c>.</summary>
    IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null);

    /// <summary>First match (or null) with an optional include shaper.</summary>
    T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null);

    Task<T?> GetByIdAsync(int id, bool includeDeleted = false);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false);

    void Create(T entity);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);

    /// <summary>Soft-deletes the entity (sets IsDeleted + DeletionDate).</summary>
    void SoftDelete(T entity);
}
