using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Users.Accounts.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Models.Config;
using Wanes.Shareds.Notifications.Sms;
using Wanes.Shareds.Security;
using Wanes.Shareds.Security.Token;

namespace Wanes.Areas.Services.Users.Accounts;

public class AccountService : IAccountService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly ITokenGenerator tokenGenerator;
    private readonly ISmsSender smsSender;
    private readonly IAuditService auditService;
    private readonly IRepository<OtpCode> otpRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<UserRole> userRoleRepository;
    private readonly IRepository<UserLogin> userLoginRepository;
    private readonly OtpSettings otpSettings;

    public AccountService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        ITokenGenerator tokenGenerator,
        ISmsSender smsSender,
        IAuditService auditService,
        IRepository<OtpCode> otpRepository,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository,
        IRepository<UserLogin> userLoginRepository,
        IOptions<OtpSettings> otpSettings)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.tokenGenerator = tokenGenerator;
        this.smsSender = smsSender;
        this.auditService = auditService;
        this.otpRepository = otpRepository;
        this.userRepository = userRepository;
        this.userRoleRepository = userRoleRepository;
        this.userLoginRepository = userLoginRepository;
        this.otpSettings = otpSettings.Value;
    }

    public async Task<BaseResponse> RequestOtp(RequestOtpInput input)
    {
        var phone = input.Phone.NormalizePhone();
        var phoneKey = phone.PhoneKey();
        // The key is what the code is later redeemed against, so it has to be whole.
        if (phoneKey.Length < PhoneExtensions.PhoneKeyLength)
            return new BaseResponse(ErrorCode.ValidationError, "Invalid phone.");

        var code = otpSettings.IsTesting ? otpSettings.FixedCode : Random.Shared.Next(1000, 9999).ToString();

        otpRepository.Create(new OtpCode
        {
            Phone = phone,
            PhoneKey = phoneKey,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(otpSettings.ExpiryMinutes),
        });
        await unitOfWork.SaveAsync();

        if (!otpSettings.IsTesting)
            await smsSender.SendAsync(phone, $"Your Wanes code is {code}");

        await auditService.LogAsync(AuditActions.OtpRequested, nameof(OtpCode));
        return new BaseResponse();
    }

    public async Task<BaseResponse<AuthResult>> VerifyOtp(VerifyOtpInput input)
    {
        var phone = input.Phone.NormalizePhone();
        var phoneKey = phone.PhoneKey();

        // Matched on the last 9 digits: the code may have been requested with the country code
        // written differently (or not at all) from how it is typed back here.
        var otp = await otpRepository
            .Where(o => o.PhoneKey == phoneKey && !o.Consumed)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (otp == null || otp.Code != input.Code)
            return new BaseResponse<AuthResult>(default, ErrorCode.InvalidOtp);
        if (otp.ExpiresAt < DateTime.UtcNow)
            return new BaseResponse<AuthResult>(default, ErrorCode.OtpExpired);

        otp.Consumed = true;
        otpRepository.Update(otp);

        // find or create the user (registration on first verified login). Looking the account up
        // by key keeps one person to one account no matter which format they sign in with.
        var user = await userRepository
            .Where(u => u.PhoneKey == phoneKey)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();
        var isNewUser = user == null;

        if (user == null)
        {
            user = new User { Phone = phone, PhoneKey = phoneKey, PhoneVerified = true, IsRider = true };
            userRepository.Create(user);
            await unitOfWork.SaveAsync();
            userRoleRepository.Create(new UserRole { UserId = user.Id, Role = Roles.User });
        }
        else if (!user.PhoneVerified)
        {
            user.PhoneVerified = true;
            userRepository.Update(user);
        }

        if (user.IsDisabled)
            return new BaseResponse<AuthResult>(default, ErrorCode.AccountDisabled);

        // new device session
        var sessionKey = Guid.NewGuid().ToString("N");
        userLoginRepository.Create(new UserLogin
        {
            UserId = user.Id,
            SessionKey = sessionKey,
            DeviceType = input.DeviceType,
            DeviceToken = input.DeviceToken,
        });

        user.LastSeenAt = DateTime.UtcNow;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();

        var roles = await LoadRoles(user.Id);

        var token = tokenGenerator.Generate(user.Id, roles, sessionKey);
        await auditService.LogAsync(AuditActions.Login, nameof(User), user.Id);

        return new BaseResponse<AuthResult>(new AuthResult
        {
            Token = token,
            IsNewUser = isNewUser,
            Profile = new ProfileOutput(user) { Roles = roles },
        });
    }

    public async Task<BaseResponse> Logout()
    {
        var sessionKey = securityManager.SessionKey;
        if (!string.IsNullOrEmpty(sessionKey))
        {
            var login = userLoginRepository.FirstOrDefault(l => l.SessionKey == sessionKey);
            if (login != null)
            {
                userLoginRepository.SoftDelete(login);
                await unitOfWork.SaveAsync();
            }
        }
        await auditService.LogAsync(AuditActions.Logout, nameof(UserLogin));
        return new BaseResponse();
    }

    public async Task<BaseResponse<ProfileOutput>> GetProfile()
    {
        var user = await GetCurrentUser();
        return new BaseResponse<ProfileOutput>(await Profile(user));
    }

    public async Task<BaseResponse<ProfileOutput>> UpdateProfile(UpdateProfileInput input)
    {
        var user = await GetCurrentUser();

        if (input.FirstName != null) user.FirstName = input.FirstName.Trim();
        if (input.LastName != null) user.LastName = input.LastName.Trim();
        if (input.DisplayName != null) user.DisplayName = input.DisplayName.Trim();
        if (input.Email != null) user.Email = input.Email.Trim();
        if (input.Gender != null) user.Gender = input.Gender.Value;
        if (input.DateOfBirth != null) user.DateOfBirth = input.DateOfBirth;
        if (input.Bio != null) user.Bio = input.Bio;

        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ProfileUpdate, nameof(User), user.Id);

        return new BaseResponse<ProfileOutput>(await Profile(user));
    }

    public async Task<BaseResponse<ProfileOutput>> UpdatePreferences(UpdatePreferencesInput input)
    {
        var user = await GetCurrentUser();

        if (input.Language != null) user.Language = input.Language.Value;
        if (input.NotifPush != null) user.NotifPush = input.NotifPush.Value;
        if (input.NotifSms != null) user.NotifSms = input.NotifSms.Value;

        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ProfilePreferences, nameof(User), user.Id);

        return new BaseResponse<ProfileOutput>(await Profile(user));
    }

    public async Task<BaseResponse<ProfileOutput>> SwitchRole(SwitchRoleInput input)
    {
        var user = await GetCurrentUser();

        // Switching into driver mode needs no admin sign-off — a driver posts and
        // publishes their own trips.
        user.ActiveRole = input.ActiveRole;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ProfileSwitchRole, nameof(User), user.Id, after: new { input.ActiveRole });

        return new BaseResponse<ProfileOutput>(await Profile(user));
    }

    /// <summary>Profile of <paramref name="user"/> with their roles attached.</summary>
    private async Task<ProfileOutput> Profile(User user) =>
        new(user) { Roles = await LoadRoles(user.Id) };

    private async Task<List<Roles>> LoadRoles(int userId)
    {
        var roles = await userRoleRepository
            .Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync();
        if (roles.Count == 0) roles.Add(Roles.User);
        return roles;
    }

    private async Task<User> GetCurrentUser()
    {
        var id = securityManager.RequireUserId();
        return await userRepository.GetByIdAsync(id)
               ?? throw new AppException(ErrorCode.NotFound);
    }
}
