using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Files;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Management;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IFileStorage fileStorage;
    private readonly ISecurityManager securityManager;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<AuditLog> auditLogRepository;
    private readonly IRepository<DriverDocument> driverDocumentRepository;

    public AdminService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        INotificationService notificationService,
        IFileStorage fileStorage,
        ISecurityManager securityManager,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<AuditLog> auditLogRepository,
        IRepository<DriverDocument> driverDocumentRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.fileStorage = fileStorage;
        this.securityManager = securityManager;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.auditLogRepository = auditLogRepository;
        this.driverDocumentRepository = driverDocumentRepository;
    }

    public async Task<BaseResponse<PageOutput<DriverRow>>> GetPendingDrivers(PageInput page, DriverStatus? status = null)
    {
        var wanted = status ?? DriverStatus.Pending;
        var query = userRepository.Where(u => u.DriverStatus == wanted);

        var total = await query.CountAsync();

        // Oldest application first: the queue is worked in the order people
        // joined it, and the driver who has waited longest is the one the wait
        // is costing.
        var users = await query
            .OrderBy(u => u.DriverAppliedAt ?? u.CreationDate)
            .ThenBy(u => u.Id)
            .Paginate(page)
            .ToListAsync();

        // One grouped count for the whole page rather than a query per row.
        var ids = users.Select(u => u.Id).ToList();
        var counts = await driverDocumentRepository
            .Where(d => ids.Contains(d.UserId) && !d.IsDeleted)
            .GroupBy(d => d.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var rows = users.Select(u => new DriverRow
        {
            Id = u.Id,
            Phone = u.Phone,
            Name = AdminLabels.ForUserName(u),
            DriverStatus = u.DriverStatus,
            LicenseNumber = u.LicenseNumber,
            AppliedAt = u.DriverAppliedAt,
            DocumentCount = counts.TryGetValue(u.Id, out var c) ? c : 0,
            ReviewNote = u.DriverReviewNote,
            ReviewedAt = u.DriverReviewedAt,
        }).ToList();

        return new BaseResponse<PageOutput<DriverRow>>(new PageOutput<DriverRow>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse<List<DriverDocumentOutput>>> GetDriverDocuments(int userId)
    {
        if (!await userRepository.AnyAsync(u => u.Id == userId))
            return BaseResponse<List<DriverDocumentOutput>>.Fail(ErrorCode.NotFound);

        var documents = await driverDocumentRepository
            .Where(d => d.UserId == userId && !d.IsDeleted)
            .OrderBy(d => d.Type)
            .ToListAsync();

        return new BaseResponse<List<DriverDocumentOutput>>(
            documents.Select(DriverDocumentOutput.From).ToList());
    }

    public async Task<BaseResponse<DocumentFile>> OpenDriverDocument(int documentId)
    {
        var document = await driverDocumentRepository.FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);
        if (document == null) return BaseResponse<DocumentFile>.Fail(ErrorCode.DriverDocumentNotFound);

        var stream = fileStorage.OpenRead(document.StorageKey);
        if (stream == null) return BaseResponse<DocumentFile>.Fail(ErrorCode.DriverDocumentNotFound);

        await auditService.LogAsync(AuditActions.AdminDriverDocumentViewed, nameof(DriverDocument), document.Id);

        return new BaseResponse<DocumentFile>(new DocumentFile
        {
            Content = stream,
            ContentType = document.ContentType,
            FileName = document.FileName,
        });
    }

    public async Task<BaseResponse> VerifyDriver(int userId, VerifyInput input)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();

        user.DriverStatus = input.Approve ? DriverStatus.Verified : DriverStatus.Rejected;
        user.DriverReviewNote = note;
        user.DriverReviewedAt = DateTime.UtcNow;
        user.DriverReviewedBy = securityManager.UserId;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(input.Approve ? AuditActions.AdminDriverVerified : AuditActions.AdminDriverRejected,
            nameof(User), userId);

        // The decision is worthless to the driver if nobody tells them: until
        // this ran, an approved driver had to keep re-opening the app to find out.
        if (input.Approve)
            await notificationService.Notify(userId, NotificationTemplate.DriverVerified,
                data: new { driverStatus = DriverStatus.Verified.ToString() });
        else
            // A rejection carries its reason when the reviewer gave one; the
            // generic wording is all there is to say when they did not.
            await notificationService.Notify(userId,
                note == null ? NotificationTemplate.DriverRejected : NotificationTemplate.DriverRejectedWithReason,
                args: new { reason = note },
                data: new { driverStatus = DriverStatus.Rejected.ToString() });

        return new BaseResponse();
    }

    public async Task<BaseResponse<PageOutput<AuditRow>>> GetAuditLog(PageInput page, int? actorUserId, string? action)
    {
        var query = auditLogRepository.Query();
        if (actorUserId != null) query = query.Where(a => a.ActorUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));

        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(a => a.Id).Paginate(page).ToListAsync();

        var names = await userRepository.ResolveNames(logs.Select(a => a.ActorUserId));

        var rows = logs.Select(a => new AuditRow
        {
            Id = a.Id,
            ActorUserId = a.ActorUserId,
            ActorName = names.NameFor(a.ActorUserId),
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Ip = a.Ip,
            CreationDate = a.CreationDate,
        }).ToList();

        return new BaseResponse<PageOutput<AuditRow>>(new PageOutput<AuditRow>
        {
            TotalRows = total,
            Data = rows,
        });
    }
}
