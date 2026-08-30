using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Accounts.Models;

public class VerifyOtpInput
{
    public string Phone { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; } = DeviceType.Android;
    public string? DeviceToken { get; set; }
}
