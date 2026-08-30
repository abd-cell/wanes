namespace Wanes.Shareds.Models.Config;

public class OtpSettings
{
    /// <summary>When true, OTP is fixed to <see cref="FixedCode"/> and no SMS is sent.</summary>
    public bool IsTesting { get; set; }
    public string FixedCode { get; set; } = "1234";
    public int ExpiryMinutes { get; set; } = 5;
}
