using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminNotificationService
{
    /// <summary>
    /// The whole table, cleared rows included — <paramref name="isDeleted"/>
    /// narrows to one side when the admin wants only one.
    /// </summary>
    Task<BaseResponse<PageOutput<NotificationRow>>> List(PageInput page, NotificationType? type, int? userId,
        bool? isRead, bool? isDeleted);
    Task<BaseResponse<NotificationRow>> Get(int id);
    Task<BaseResponse<NotificationRow>> Create(NotificationInput input);

    /// <summary>Sends one notification to an entire audience. Returns the recipient count.</summary>
    Task<BaseResponse<BroadcastResult>> Broadcast(BroadcastInput input);
    /// <summary>Sends one notification to a hand-picked set of users. Returns the recipient count.</summary>
    Task<BaseResponse<TargetedSendResult>> SendTargeted(TargetedSendInput input);

    /// <summary>Marks read/unread or deletes every row the admin ticked in the table.</summary>
    Task<BaseResponse<BulkNotificationResult>> Bulk(BulkNotificationInput input);

    /// <summary>Headline counts describing the whole table, not one page of it.</summary>
    Task<BaseResponse<NotificationStats>> Stats();

    Task<BaseResponse<NotificationRow>> Update(int id, NotificationInput input);
    Task<BaseResponse> Delete(int id);
}
