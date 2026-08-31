using Wanes.Areas.Services.Support.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Support;

[ScopedInjectable]
public interface IFaqService
{
    /// <summary>
    /// The published FAQ, grouped-ready: ordered by category then sort order.
    /// Never fails — an empty table yields an empty list.
    /// </summary>
    Task<BaseResponse<FaqOutput>> Get();
}
