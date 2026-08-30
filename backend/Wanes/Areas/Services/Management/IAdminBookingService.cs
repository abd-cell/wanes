using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminBookingService
{
    Task<BaseResponse<PageOutput<BookingRow>>> List(PageInput page, BookingStatus? status, int? tripId, int? riderId);
    Task<BaseResponse<BookingRow>> Get(int id);
    Task<BaseResponse<BookingRow>> Create(BookingInput input);
    Task<BaseResponse<BookingRow>> Update(int id, BookingInput input);
    Task<BaseResponse> Delete(int id);
}
