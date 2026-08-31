using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Services.Support.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Support;

/// <summary>
/// Read-only view of the FAQ for the clients. Admin editing lives in
/// <see cref="Management.AdminFaqService"/>.
/// </summary>
public class FaqService : IFaqService
{
    private readonly IRepository<FaqItem> repository;

    public FaqService(IRepository<FaqItem> repository) => this.repository = repository;

    public async Task<BaseResponse<FaqOutput>> Get()
    {
        var items = await repository.Query()
            .Where(x => x.IsPublished)
            .OrderBy(x => x.Category)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return new BaseResponse<FaqOutput>(new FaqOutput
        {
            Items = items.Select(x => new FaqItemOutput(x)).ToList(),
            // Max of both columns: an edited row carries ModificationDate, a
            // freshly created one only CreationDate.
            UpdatedAt = items.Count == 0
                ? null
                : items.Max(x => x.ModificationDate ?? x.CreationDate),
        });
    }
}
