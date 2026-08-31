using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models.Config;

namespace Wanes.Shareds.Notifications.Fcm;

/// <summary>
/// Sends push notifications through Firebase Cloud Messaging (HTTP v1).
///
/// Credentials are optional by design: with no service account configured the
/// sender logs what it *would* have sent and reports success, so the API runs
/// unchanged on a dev machine that has no Firebase project. See
/// <see cref="FcmSettings"/> for the two ways to supply the key.
/// </summary>
[SingletonInjectable]
public class FcmSender : IFcmSender
{
    /// <summary>FCM caps a multicast at 500 tokens per call.</summary>
    private const int BatchSize = 500;

    /// <summary>Must match the channel the Flutter app creates, or Android 8+ drops the notification.</summary>
    private const string AndroidChannelId = "wanes_default";

    private readonly ILogger<FcmSender> logger;
    private readonly FirebaseMessaging? messaging;

    public bool IsConfigured => messaging != null;

    public FcmSender(ILogger<FcmSender> logger, IOptions<FcmSettings> options, IHostEnvironment environment)
    {
        this.logger = logger;

        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("FCM disabled by configuration — notifications will be logged only.");
            return;
        }

        try
        {
            var (credential, projectId) = LoadCredential(settings, environment);
            if (credential == null)
            {
                logger.LogWarning(
                    "FCM has no service account (set Fcm:CredentialsPath or Fcm:CredentialsJson) — " +
                    "notifications will be logged only.");
                return;
            }

            // FirebaseApp is process-wide and may only be created once.
            // ProjectId is set explicitly: the v1 send endpoint embeds it in the
            // URL, and leaving it to FirebaseAdmin's fallback chain (env vars,
            // then the credential) makes a misconfigured host fail at send time
            // rather than at boot.
            var app = FirebaseApp.DefaultInstance
                      ?? FirebaseApp.Create(new AppOptions
                      {
                          Credential = credential,
                          ProjectId = projectId,
                      });
            messaging = FirebaseMessaging.GetMessaging(app);
            logger.LogInformation("FCM ready (project {ProjectId}).",
                app.Options.ProjectId ?? "resolved by SDK");
        }
        catch (Exception ex)
        {
            // A bad key must not stop the API from booting — push just stays off.
            logger.LogError(ex, "FCM initialisation failed — notifications will be logged only.");
        }
    }

    /// <summary>
    /// Loads the service account and the project it belongs to. A null credential
    /// means "no key configured" — push is then off, which is not an error.
    /// </summary>
    private static (GoogleCredential? Credential, string? ProjectId) LoadCredential(
        FcmSettings settings, IHostEnvironment environment)
    {
        // Firebase authenticates as a service account, so the key is always a
        // service-account JSON — hence the explicit credential type.
        if (!string.IsNullOrWhiteSpace(settings.CredentialsJson))
        {
            var account = CredentialFactory.FromJson<ServiceAccountCredential>(settings.CredentialsJson);
            return (account.ToGoogleCredential(), account.ProjectId);
        }

        if (!string.IsNullOrWhiteSpace(settings.CredentialsPath))
        {
            var path = Path.IsPathRooted(settings.CredentialsPath)
                ? settings.CredentialsPath
                : Path.Combine(environment.ContentRootPath, settings.CredentialsPath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"FCM credentials file not found: {path}");

            var account = CredentialFactory.FromFile<ServiceAccountCredential>(path);
            return (account.ToGoogleCredential(), account.ProjectId);
        }

        // Falls back to GOOGLE_APPLICATION_CREDENTIALS / workload identity when the
        // host provides one; returns null (push off) when it doesn't. The project
        // then comes from the environment, which is how those hosts supply it.
        try
        {
            return (GoogleCredential.GetApplicationDefault(),
                Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT"));
        }
        catch
        {
            return (null, null);
        }
    }

    public async Task<FcmSendResult> SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null)
    {
        var tokens = deviceTokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct()
            .ToList();
        if (tokens.Count == 0) return FcmSendResult.None;

        if (messaging == null)
        {
            logger.LogInformation("FCM (not configured) → {Count} device(s): {Title} — {Body}",
                tokens.Count, title, body);
            return new FcmSendResult { SuccessCount = tokens.Count };
        }

        var success = 0;
        var failure = 0;
        var invalid = new List<string>();

        foreach (var batch in Chunk(tokens))
        {
#pragma warning disable CS0618 // see the note on Tokens below
            var message = new MulticastMessage
            {
                // `Tokens` is flagged obsolete in favour of `Fids`, but FIDs are
                // installation ids — the app registers FCM registration tokens
                // (`FirebaseMessaging.getToken`), which is what this property takes.
                Tokens = batch,
                Notification = new Notification { Title = title, Body = body },
                Data = data?.AsReadOnly(),
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ChannelId = AndroidChannelId,
                        // Resource name only — the drawable ships with the app.
                        Icon = "ic_notification",
                        Sound = "default",
                    },
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps { Sound = "default", ContentAvailable = true },
                    Headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                },
            };
#pragma warning restore CS0618

            try
            {
                var response = await messaging.SendEachForMulticastAsync(message);
                success += response.SuccessCount;
                failure += response.FailureCount;

                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var item = response.Responses[i];
                    if (item.IsSuccess) continue;

                    var code = item.Exception?.MessagingErrorCode;
                    if (code is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
                        invalid.Add(batch[i]);
                    else
                        logger.LogWarning("FCM send failed for one device: {Error}", item.Exception?.Message);
                }
            }
            catch (Exception ex)
            {
                // Network/quota failures: count them, but never surface to the caller.
                failure += batch.Count;
                logger.LogError(ex, "FCM batch send failed for {Count} device(s).", batch.Count);
            }
        }

        if (invalid.Count > 0)
            logger.LogInformation("FCM reported {Count} stale device token(s) for pruning.", invalid.Count);

        return new FcmSendResult { SuccessCount = success, FailureCount = failure, InvalidTokens = invalid };
    }

    private static IEnumerable<List<string>> Chunk(List<string> tokens)
    {
        for (var i = 0; i < tokens.Count; i += BatchSize)
            yield return tokens.GetRange(i, Math.Min(BatchSize, tokens.Count - i));
    }
}
