using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Models.Base;

namespace Wanes.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IUnitOfWork"/>. Each entity type gets one shared list.
///
/// The transaction is real enough to be worth trusting: <see cref="BeginTransactionAsync"/>
/// snapshots every store and <see cref="RollBackAsync"/> puts it back, both the
/// membership and the scalar values. That matters because the services stage
/// several writes before committing — a booking plus the seats coming off the
/// trip, a trip plus a booking plus the hail's claim — and a rollback that left
/// them behind would make a retry see rows the database would have discarded.
/// (A no-op rollback made the seat-race retry find its own abandoned booking and
/// answer AlreadyBooked, which is not a thing that can happen.)
/// </summary>
public class FakeUnitOfWork : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repos = [];
    private readonly Dictionary<Type, object> _stores = [];

    private List<Action>? _rollbacks;
    private Action? _pendingWinnerWrite;

    public int SaveCount { get; private set; }

    /// <summary>How many times <see cref="Detach"/> was called — a retry's fingerprint.</summary>
    public int DetachCount { get; private set; }

    /// <summary>
    /// Makes the next N commits lose a row-version race.
    ///
    /// The guarantee itself lives in SQL Server: the UPDATE carries
    /// <c>AND RowVersion = @old</c> and changes no rows once it has been
    /// overtaken. Nothing in memory reproduces that, so what this reproduces is
    /// the *consequence* — the <see cref="DbUpdateConcurrencyException"/> EF
    /// raises — which is what the retry paths are written against.
    /// </summary>
    public int LoseNextCommits { get; set; }

    /// <summary>
    /// Whatever the *winner* of that race wrote: the seat taken, the hail
    /// claimed. Applied after the loser's rollback rather than before it, which
    /// is the order that actually happened — the winner committed, so its write
    /// is not the loser's to undo. Running it earlier would let the rollback
    /// erase it and hand the retry back the world it had already lost.
    /// </summary>
    public Action? OnLostCommit { get; set; }

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

    public Task BeginTransactionAsync()
    {
        _rollbacks = [];
        foreach (var store in _stores.Values)
        {
            var list = (IList)store;
            var members = list.Cast<object>().ToList();
            var values = members.Select(Snapshot).ToList();

            _rollbacks.Add(() =>
            {
                list.Clear();
                for (var i = 0; i < members.Count; i++)
                {
                    Restore(members[i], values[i]);
                    list.Add(members[i]);
                }
            });
        }
        return Task.CompletedTask;
    }

    public Task CommitAsync()
    {
        if (LoseNextCommits > 0)
        {
            LoseNextCommits--;
            _pendingWinnerWrite = OnLostCommit;
            throw new DbUpdateConcurrencyException("simulated row-version conflict");
        }
        SaveCount++;
        _rollbacks = null;
        return Task.CompletedTask;
    }

    public Task RollBackAsync()
    {
        if (_rollbacks != null)
        {
            foreach (var undo in _rollbacks) undo();
            _rollbacks = null;
        }

        var winner = _pendingWinnerWrite;
        _pendingWinnerWrite = null;
        winner?.Invoke();

        return Task.CompletedTask;
    }

    /// <summary>
    /// No identity map here — the fake repositories read the seeded lists
    /// directly — so there is nothing to forget. Counted rather than ignored
    /// because it is how a test tells a retry from a single attempt.
    /// </summary>
    public void Detach() => DetachCount++;

    // ── Snapshotting ──
    //
    // Scalars only. Navigation properties and geometry are arranged once, up
    // front, and no service mutates them inside a transaction — whereas seat
    // counts, statuses and foreign keys are exactly what a rollback has to undo.

    private static Dictionary<PropertyInfo, object?> Snapshot(object entity) =>
        ScalarProperties(entity.GetType())
            .ToDictionary(p => p, p => p.GetValue(entity));

    private static void Restore(object entity, Dictionary<PropertyInfo, object?> values)
    {
        foreach (var (property, value) in values) property.SetValue(entity, value);
    }

    private static IEnumerable<PropertyInfo> ScalarProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType));

    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
               || t.IsEnum
               || t == typeof(string)
               || t == typeof(decimal)
               || t == typeof(DateTime)
               || t == typeof(DateTimeOffset)
               || t == typeof(TimeSpan)
               || t == typeof(Guid)
               || t == typeof(byte[]);
    }
}
