using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Search;

[AppAuthorize]
public class SearchController : BaseApiController
{
    private readonly ISearchService searchService;

    public SearchController(ISearchService searchService) => this.searchService = searchService;

    /// <summary>Rider searches a trip; returns matching trips (carpool) or opens a request (hail).</summary>
    [HttpPost]
    public async Task<BaseResponse<SearchResult>> Search([FromBody] SearchInput input)
        => await searchService.Search(input);
}
