using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>A phone verification / login one-time code. Never stored raw in the audit log.</summary>
public class OtpCode : BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public string PhoneKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public int Attempts { get; set; }
}
