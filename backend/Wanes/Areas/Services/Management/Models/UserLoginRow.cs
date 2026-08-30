using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of an active device session (list + detail share one shape).</summary>
public class UserLoginRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? OwnerName { get; set; }
    public string SessionKey { get; set; } = string.Empty;
    public DeviceType DeviceType { get; set; }
    public string? DeviceToken { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime CreationDate { get; set; }

    public UserLoginRow() { }

    public UserLoginRow(UserLogin e)
    {
        Id = e.Id;
        UserId = e.UserId;
        SessionKey = e.SessionKey;
        DeviceType = e.DeviceType;
        DeviceToken = e.DeviceToken;
        LastActivityAt = e.LastActivityAt;
        CreationDate = e.CreationDate;
    }
}
