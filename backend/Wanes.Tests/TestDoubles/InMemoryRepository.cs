using System.Linq.Expressions;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Models.Base;

namespace Wanes.Tests.TestDoubles;

/// <summary>List-backed <see cref="IRepository{T}"/> with async-capable queries.</summary>
public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly List<T> _store;
    private int _seq;

    public InMemoryRepository(List<T> store)
    {
        _store = store;
        _seq = store.Count == 0 ? 0 : store.Max(x => x.Id);
    }

    public IQueryable<T> Query(bool includeDeleted = false)
    {
        var items = includeDeleted ? _store : _store.Where(x => !x.IsDeleted);
        return new TestAsyncEnumerable<T>(items);
    }

    // Include shapers are EF-only; in-memory tests set up navigations directly, so we ignore them.
    public IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null) =>
        Query().Where(predicate);

    public T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null) =>
        Query().Where(predicate.Compile()).FirstOrDefault();

    public void Create(T entity)
    {
        if (entity.Id == 0) entity.Id = ++_seq;
        _store.Add(entity);
    }

    public Task<T?> GetByIdAsync(int id, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).FirstOrDefault(x => x.Id == id));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).FirstOrDefault(predicate.Compile()));

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).Any(predicate.Compile()));

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false) =>
        Task.FromResult(predicate is null
            ? Query(includeDeleted).Count()
            : Query(includeDeleted).Count(predicate.Compile()));

    public Task AddAsync(T entity)
    {
        if (entity.Id == 0) entity.Id = ++_seq;
        _store.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<T> entities)
    {
        foreach (var e in entities) { if (e.Id == 0) e.Id = ++_seq; _store.Add(e); }
        return Task.CompletedTask;
    }

    public void Update(T entity) { /* mutations happen in place on the shared list */ }

    public void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        entity.DeletionDate = DateTime.UtcNow;
    }
}
