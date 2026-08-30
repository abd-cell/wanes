using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications.Fcm;
using Wanes.Shareds.Security;
using Wanes.Shareds.SSE;

namespace Wanes.Areas.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IFcmSender fcmSender;
    private readonly SseConnectionManager sseConnectionManager;
    private readonly IRepository<UserNotification> notificationRepository;
    private readonly IRepository<UserLogin> userLoginRepository;
    private readonly IRepository<User> userRepository;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IFcmSender fcmSender,
        SseConnectionManager sseConnectionManager,
        IRepository<UserNotification> notificationRepository,
        IRepository<UserLogin> userLoginRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.fcmSender = fcmSender;
        this.sseConnectionManager = sseConnectionManager;
        this.notificationRepository = notificationRepository;
        this.userLoginRepository = userLoginRepository;
        this.userRepository = userRepository;
    }

    public async Task Notify(int userId, NotificationType type, string title, string body, object? data = null)
    {
        var dataJson = data == null ? null : JsonSerializer.Serialize(data);

        notificationRepository.Create(new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DataJson = dataJson,
        });
        await unitOfWork.SaveAsync();

        var tokens = await userLoginRepository
            .Where(l => l.UserId == userId && l.DeviceToken != null)
            .Select(l => l.DeviceToken!).ToListAsync();
        await fcmSender.SendAsync(tokens, title, body);

        var payload = JsonSerializer.Serialize(new { type = type.ToString(), title, body, data });
        await sseConnectionManager.SendAsync(userId, payload);
    }

    public async Task<int> NotifyNearbyDrivers(RideRequest request)
    {
        var driverIds = await userRepository
            .Where(u => u.IsDriver
                        && u.DriverStatus == DriverStatus.Verified
                        && u.IsOnline
                        && u.LastLocation != null
                        && u.Id != request.RiderId
                        && u.LastLocation!.IsWithinDistance(request.Origin, request.RadiusMeters))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var driverId in driverIds)
        {
            await Notify(driverId, NotificationType.RideRequestNearby,
                "New ride nearby",
                $"{request.OriginAddress} → {request.DestinationAddress}",
                new { requestId = request.Id, seats = request.Seats });
        }

        return driverIds.Count;
    }

    public async Task<BaseResponse<List<NotificationRow>>> GetUserNotifications()
    {
        var userId = securityManager.RequireUserId();
        var notifications = await notificationRepository
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id).Take(50).ToListAsync();

        var data = notifications.Select(n => new NotificationRow(n)).ToList();
        return new BaseResponse<List<NotificationRow>>(data);
    }

    public async Task<BaseResponse> MarkRead(int id)
    {
        var userId = securityManager.RequireUserId();
        var notification = notificationRepository.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (notification == null) return new BaseResponse(ErrorCode.NotFound);

        notification.IsRead = true;
        notificationRepository.Update(notification);
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }
}
