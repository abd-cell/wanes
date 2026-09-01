using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Areas.Services.Users.Presence.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Presence;

/// <summary>Driver presence: last known location + online flag (used for hail targeting).</summary>
public class PresenceService : IPresenceService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IRepository<User> userRepository;

    public PresenceService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IDriverAvailabilityService driverAvailabilityService,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.driverAvailabilityService = driverAvailabilityService;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse> Update(UpdateLocationInput input)
    {
        var user = await userRepository.GetByIdAsync(securityManager.RequireUserId());
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        // The location is always taken — riders track the car they are in, and
        // that matters most while the driver is driving. Going *online* is what a
        // trip blocks: a driver already on the road is not available for another,
        // so the flag stays down however often the app reports itself online.
        var engaged = await driverAvailabilityService.IsEngaged(user.Id);

        user.LastLocation = GeoFactory.Point(input.Lat, input.Lng);
        user.LastLocationAt = DateTime.UtcNow;
        user.IsOnline = input.Online && !engaged;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse> GoOffline()
    {
        var user = await userRepository.GetByIdAsync(securityManager.RequireUserId());
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        user.IsOnline = false;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }
}
