namespace Wanes.Shareds.Enums;

/// <summary>
/// Light/dark preference, stored per account so the choice follows the user
/// onto a new device instead of living only in that handset's memory.
/// </summary>
public enum AppTheme
{
    /// <summary>Follow the device setting — what an account starts on.</summary>
    System = 0,
    Light = 1,
    Dark = 2,
}
