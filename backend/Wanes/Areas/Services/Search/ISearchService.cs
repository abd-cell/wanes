using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Search;

[TransientInjectable]
public interface ISearchService
{
    Task<BaseResponse<SearchResult>> Search(SearchInput input);
}
