using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Driver;

public class DriverOnboardingService : IDriverOnboardingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<User> userRepository;

    public DriverOnboardingService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse> Apply(DriverApplyInput input)
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        if (string.IsNullOrWhiteSpace(input.LicenseNumber))
            return new BaseResponse(ErrorCode.ValidationError, "License number is required.");

        user.IsDriver = true;
        user.LicenseNumber = input.LicenseNumber.Trim();
        user.LicensePhotoUrl = input.LicensePhotoUrl;
        user.IdDocumentUrl = input.IdDocumentUrl;
        user.DriverStatus = DriverStatus.Pending;

        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DriverApply, nameof(User), user.Id);
        return new BaseResponse();
    }
}
