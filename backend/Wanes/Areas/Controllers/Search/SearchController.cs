using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Series;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Search;

[AppAuthorize]
public class SearchController : BaseApiController
{
    private readonly ISearchService searchService;
    private readonly ISeriesInfoService seriesInfo;

    public SearchController(ISearchService searchService, ISeriesInfoService seriesInfo)
    {
        this.searchService = searchService;
        this.seriesInfo = seriesInfo;
    }

    /// <summary>Rider searches a trip; returns matching trips (carpool) or opens a request (hail).</summary>
    [HttpPost]
    public async Task<BaseResponse<SearchResult>> Search([FromBody] SearchInput input)
        => await seriesInfo.With(await searchService.Search(input));
}
