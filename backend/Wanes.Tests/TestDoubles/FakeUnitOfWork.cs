using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Models.Base;

namespace Wanes.Tests.TestDoubles;

/// <summary>In-memory <see cref="IUnitOfWork"/>. Each entity type gets one shared list.</summary>
public class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repos = [];
    private readonly Dictionary<Type, object> _stores = [];

    public int SaveCount { get; private set; }

    /// <summary>Seeds the store for an entity type (used by test arrange steps).</summary>
    public List<T> Store<T>() where T : BaseEntity
    {
        if (!_stores.TryGetValue(typeof(T), out var store))
        {
            store = new List<T>();
            _stores[typeof(T)] = store;
        }
        return (List<T>)store;
    }

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (_repos.TryGetValue(typeof(T), out var repo)) return (IRepository<T>)repo;
        var created = new InMemoryRepository<T>(Store<T>());
        _repos[typeof(T)] = created;
        return created;
    }

    public Task<int> SaveAsync() { SaveCount++; return Task.FromResult(0); }

    public Task BeginTransactionAsync() => Task.CompletedTask;
    public Task CommitAsync() { SaveCount++; return Task.CompletedTask; }
    public Task RollBackAsync() => Task.CompletedTask;
}
