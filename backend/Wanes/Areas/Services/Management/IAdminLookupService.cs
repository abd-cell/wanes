using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminLookupService
{
    /// <summary>Searchable option list for a reference picker (users, trips, bookings, ...).</summary>
    Task<BaseResponse<PageOutput<LookupRow>>> Search(string type, PageInput page);

    /// <summary>Resolves a single stored foreign key back to its label, for the edit form.</summary>
    Task<BaseResponse<LookupRow>> Get(string type, int id);
}
