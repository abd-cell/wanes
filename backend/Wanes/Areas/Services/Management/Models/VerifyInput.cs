using System.ComponentModel.DataAnnotations;

namespace Wanes.Areas.Services.Management.Models;

public class VerifyInput
{
    public bool Approve { get; set; } = true;

    /// <summary>
    /// The reviewer's reason, sent on to the driver. Optional on an approval,
    /// where there is nothing to explain; on a rejection it is the difference
    /// between a driver who fixes the photo and one who re-sends the same one.
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}
