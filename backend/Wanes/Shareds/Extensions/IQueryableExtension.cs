using Wanes.Shareds.Models;

namespace Wanes.Shareds.Extensions;

public static class IQueryableExtension
{
    /// <summary>Applies 1-based paging (Skip/Take) to a query.</summary>
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, PageInput input) =>
        query.Skip(input.Skip).Take(input.PageSize);

    /// <summary>Excludes soft-deleted rows (there is no global query filter).</summary>
    public static IQueryable<T> NotDeleted<T>(this IQueryable<T> query)
        where T : Models.Base.BaseEntity =>
        query.Where(x => !x.IsDeleted);
}
