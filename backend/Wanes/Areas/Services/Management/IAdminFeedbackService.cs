using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminFeedbackService
{
    Task<BaseResponse<PageOutput<FeedbackRow>>> List(PageInput page, FeedbackKind? kind, FeedbackStatus? status);
    Task<BaseResponse<FeedbackRow>> Get(int id);

    /// <summary>
    /// Moves a submission along and optionally answers it. Notifies the user
    /// when the reply text actually changed.
    /// </summary>
    Task<BaseResponse<FeedbackRow>> Update(int id, FeedbackReviewInput input);

    Task<BaseResponse> Delete(int id);
}
