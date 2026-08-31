namespace Wanes.Shareds.Models.Config;

/// <summary>
/// Firebase Cloud Messaging credentials. Push is optional — when no service
/// account is configured the sender degrades to logging, so local development
/// and CI run without a Firebase project.
/// </summary>
public class FcmSettings
{
    /// <summary>Master switch. Set false to silence push even with credentials present.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Path to the service-account JSON downloaded from the Firebase console
    /// (Project settings → Service accounts → Generate new private key).
    /// Relative paths resolve against the content root.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// The same service-account JSON inline, for hosts that inject secrets as
    /// environment variables rather than files (<c>Fcm__CredentialsJson</c>).
    /// Takes precedence over <see cref="CredentialsPath"/>.
    /// </summary>
    public string? CredentialsJson { get; set; }
}
