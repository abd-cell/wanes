using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// The support desk's inbox. Users file through
/// <see cref="Support.FeedbackService"/> and read the answers there.
///
/// Read, reply and close — never create, and the user's own words are never
/// edited. Deleting is soft, like everywhere else, so a removed row is still
/// there when someone asks what the platform was told.
/// </summary>
public class AdminFeedbackService : IAdminFeedbackService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<Feedback> feedbackRepository;
    private readonly IRepository<User> userRepository;

    public AdminFeedbackService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<Feedback> feedbackRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.feedbackRepository = feedbackRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<PageOutput<FeedbackRow>>> List(
        PageInput page, FeedbackKind? kind, FeedbackStatus? status)
    {
        var query = feedbackRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(f => f.Subject.Contains(term) || f.Message.Contains(term));
        }
        if (kind != null) query = query.Where(f => f.Kind == kind);
        if (status != null) query = query.Where(f => f.Status == status);

        var total = await query.CountAsync();

        // Newest first. Open-before-closed would look tidier but reorders the
        // list under the admin the moment they resolve something, and a desk
        // works a queue by arrival.
        var items = await query.OrderByDescending(f => f.Id).Paginate(page).ToListAsync();

        var rows = await Rows(items);
        return new BaseResponse<PageOutput<FeedbackRow>>(
            new PageOutput<FeedbackRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<FeedbackRow>> Get(int id)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id);
        if (feedback == null) return new BaseResponse<FeedbackRow>(default, ErrorCode.FeedbackNotFound);

        var rows = await Rows([feedback]);
        return new BaseResponse<FeedbackRow>(rows[0]);
    }

    public async Task<BaseResponse<FeedbackRow>> Update(int id, FeedbackReviewInput input)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id);
        if (feedback == null) return new BaseResponse<FeedbackRow>(default, ErrorCode.FeedbackNotFound);

        var before = new FeedbackRow(feedback);
        var reply = input.Reply?.Trim();

        // A blank reply is "no comment yet", not "delete what I wrote": the
        // status dropdown and the reply box submit together, so closing a
        // thread whose answer is already written must not blank it.
        var isNewReply = !string.IsNullOrEmpty(reply) && reply != feedback.Reply;

        feedback.Status = input.Status;
        if (isNewReply)
        {
            feedback.Reply = reply;
            feedback.RepliedBy = securityManager.RequireUserId();
            feedback.RepliedAt = DateTime.UtcNow;
        }

        feedbackRepository.Update(feedback);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync("admin.feedback.update", nameof(Feedback), feedback.Id,
            before, new FeedbackRow(feedback));

        // Only the answer is worth a push. A status moving New → InReview is
        // desk bookkeeping, and notifying on it would train the user to ignore
        // the one notification that carries an actual answer.
        if (isNewReply)
            await notificationService.Notify(feedback.UserId, NotificationTemplate.FeedbackReplied,
                args: new { subject = feedback.Subject },
                data: new { feedbackId = feedback.Id });

        return await Get(feedback.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var feedback = await feedbackRepository.GetByIdAsync(id);
        if (feedback == null) return new BaseResponse(ErrorCode.FeedbackNotFound);

        var before = new FeedbackRow(feedback);

        feedbackRepository.SoftDelete(feedback);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.feedback.delete", nameof(Feedback), id, before, null);

        return new BaseResponse();
    }

    /// <summary>
    /// Rows with the author's and the answering admin's names resolved in one
    /// query — <see cref="Feedback.RepliedBy"/> is a bare id with no navigation
    /// property, and the grid has to show a person, not a number.
    /// </summary>
    private async Task<List<FeedbackRow>> Rows(IReadOnlyCollection<Feedback> items)
    {
        var names = await userRepository.ResolveNames(
            items.Select(f => (int?)f.UserId).Concat(items.Select(f => f.RepliedBy)));

        return items.Select(f => new FeedbackRow(f)
        {
            UserName = names.NameFor(f.UserId),
            RepliedByName = names.NameFor(f.RepliedBy),
        }).ToList();
    }
}
