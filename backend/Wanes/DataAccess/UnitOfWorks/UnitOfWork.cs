using Microsoft.EntityFrameworkCore.Storage;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models.Base;

namespace Wanes.DataAccess.UnitOfWorks;

[ScopedInjectable]
public class UnitOfWork : IUnitOfWork
{
    private readonly DatabaseService _db;
    private readonly Dictionary<Type, object> _repositories = [];
    private IDbContextTransaction? _transaction;

    public UnitOfWork(DatabaseService db) => _db = db;

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (_repositories.TryGetValue(typeof(T), out var existing))
            return (IRepository<T>)existing;

        var repo = new Repository<T>(_db);
        _repositories[typeof(T)] = repo;
        return repo;
    }

    public Task<int> SaveAsync() => _db.SaveChangesAsync();

    public async Task BeginTransactionAsync() =>
        _transaction = await _db.Database.BeginTransactionAsync();

    public async Task CommitAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
            if (_transaction is not null) await _transaction.CommitAsync();
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollBackAsync()
    {
        if (_transaction is not null) await _transaction.RollbackAsync();
        await DisposeTransactionAsync();
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
