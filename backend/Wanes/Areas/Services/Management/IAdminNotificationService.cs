using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminNotificationService
{
    Task<BaseResponse<PageOutput<NotificationRow>>> List(PageInput page, NotificationType? type, int? userId, bool? isRead);
    Task<BaseResponse<NotificationRow>> Get(int id);
    Task<BaseResponse<NotificationRow>> Create(NotificationInput input);
    Task<BaseResponse<NotificationRow>> Update(int id, NotificationInput input);
    Task<BaseResponse> Delete(int id);
}
