using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<UserNotification> notificationRepository;
    private readonly IRepository<User> userRepository;

    public AdminNotificationService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<UserNotification> notificationRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.notificationRepository = notificationRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<PageOutput<NotificationRow>>> List(PageInput page, NotificationType? type, int? userId, bool? isRead)
    {
        var query = notificationRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));
        }
        if (type != null) query = query.Where(n => n.Type == type);
        if (userId != null) query = query.Where(n => n.UserId == userId);
        if (isRead != null) query = query.Where(n => n.IsRead == isRead);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(n => n.Id).Paginate(page).ToListAsync();

        var rows = await BuildRows(items);

        return new BaseResponse<PageOutput<NotificationRow>>(new PageOutput<NotificationRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<NotificationRow>> Get(int id)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse<NotificationRow>(default, ErrorCode.NotFound);
        return new BaseResponse<NotificationRow>((await BuildRows([notification])).First());
    }

    public async Task<BaseResponse<NotificationRow>> Create(NotificationInput input)
    {
        var notification = new UserNotification();
        Apply(notification, input);
        notificationRepository.Create(notification);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.notifications.create", nameof(UserNotification), notification.Id);
        return await Get(notification.Id);
    }

    public async Task<BaseResponse<NotificationRow>> Update(int id, NotificationInput input)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse<NotificationRow>(default, ErrorCode.NotFound);

        Apply(notification, input);
        notificationRepository.Update(notification);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.notifications.update", nameof(UserNotification), notification.Id);
        return await Get(notification.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse(ErrorCode.NotFound);

        notificationRepository.SoftDelete(notification);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.notifications.delete", nameof(UserNotification), id);
        return new BaseResponse();
    }

    /// <summary>Attaches the recipient name; UserId has no navigation property.</summary>
    private async Task<List<NotificationRow>> BuildRows(IReadOnlyCollection<UserNotification> items)
    {
        var names = await userRepository.ResolveNames(items.Select(n => (int?)n.UserId));
        return items.Select(n => new NotificationRow(n) { UserName = names.NameFor(n.UserId) }).ToList();
    }

    private static void Apply(UserNotification notification, NotificationInput input)
    {
        notification.UserId = input.UserId;
        notification.Type = input.Type;
        notification.Title = input.Title;
        notification.Body = input.Body;
        notification.DataJson = input.DataJson;
        notification.IsRead = input.IsRead;
    }
}
