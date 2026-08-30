using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wanes.Shareds.Models.Base;

namespace Wanes.DataAccess.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DatabaseService _db;
    private readonly DbSet<T> _set;

    public Repository(DatabaseService db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public IQueryable<T> Query(bool includeDeleted = false)
    {
        IQueryable<T> q = _set.AsQueryable();
        return includeDeleted ? q : q.Where(x => !x.IsDeleted);
    }

    public IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = Query();
        if (include != null) query = include(query);
        return query.Where(predicate);
    }

    public T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = Query();
        if (include != null) query = include(query);
        return query.FirstOrDefault(predicate);
    }

    public void Create(T entity) => _set.Add(entity);

    public Task<T?> GetByIdAsync(int id, bool includeDeleted = false) =>
        Query(includeDeleted).FirstOrDefaultAsync(x => x.Id == id);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Query(includeDeleted).FirstOrDefaultAsync(predicate);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Query(includeDeleted).AnyAsync(predicate);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false) =>
        predicate is null ? Query(includeDeleted).CountAsync() : Query(includeDeleted).CountAsync(predicate);

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);

    public async Task AddRangeAsync(IEnumerable<T> entities) => await _set.AddRangeAsync(entities);

    public void Update(T entity)
    {
        entity.ModificationDate = DateTime.UtcNow;
        _set.Update(entity);
    }

    public void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        entity.DeletionDate = DateTime.UtcNow;
        _set.Update(entity);
    }
}
