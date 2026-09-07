using Wanes.Areas.Services.Support.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Support;

[ScopedInjectable]
public interface IFeedbackService
{
    /// <summary>
    /// Files a complaint or a suggestion for the calling user.
    ///
    /// Fails with <see cref="ErrorCode.TooManyOpenFeedback"/> once the account
    /// has <see cref="Domain.Support.FeedbackRules.MaxOpenPerUser"/> submissions
    /// still waiting on the desk, and with <see cref="ErrorCode.TripNotFound"/>
    /// if a trip is named that is not one of the caller's.
    /// </summary>
    Task<BaseResponse<FeedbackOutput>> Submit(FeedbackInput input);

    /// <summary>
    /// The caller's own submissions, newest first, each with the desk's reply
    /// when one has been written. Never fails — nothing submitted yields an
    /// empty list.
    /// </summary>
    Task<BaseResponse<List<FeedbackOutput>>> Mine();
}
