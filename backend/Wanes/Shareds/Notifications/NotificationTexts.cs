using System.Text;
using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Notifications;

/// <summary>
/// The English and Arabic wording for every <see cref="NotificationTemplate"/>,
/// and the type each one is filed under.
///
/// Both languages are rendered for every notification and both are stored on the
/// row, so the inbox can follow the user's current language with no round trip
/// and no stale text after a language switch — the same reasoning as
/// <c>FaqItem</c>. Push is the exception: an FCM payload carries one title and
/// one body, so the sender picks using the recipient's <c>User.Language</c>.
///
/// Placeholders are <c>{name}</c>-style and substituted from the caller's args
/// object. An unknown placeholder is left as-is rather than throwing: a missing
/// argument should cost one ugly notification, never a failed booking.
/// </summary>
public static class NotificationTexts
{
    private sealed record Entry(NotificationType Type, string TitleEn, string BodyEn, string TitleAr, string BodyAr);

    /// <summary>
    /// Arabic is phrased naturally rather than transliterated: routes read
    /// "من X إلى Y" instead of borrowing the English "X -> Y" arrow, which reads
    /// backwards in an RTL line.
    /// </summary>
    private static readonly Dictionary<NotificationTemplate, Entry> Templates = new()
    {
        [NotificationTemplate.RideRequestNearbyDriver] = new(
            NotificationType.RideRequestNearby,
            "New ride nearby", "{origin} -> {destination}",
            "رحلة جديدة قريبة", "من {origin} إلى {destination}"),

        [NotificationTemplate.BookingConfirmedRider] = new(
            NotificationType.BookingConfirmed,
            "Booking confirmed", "{origin} -> {destination}",
            "تم تأكيد الحجز", "من {origin} إلى {destination}"),

        [NotificationTemplate.BookingConfirmedDriver] = new(
            NotificationType.BookingConfirmed,
            "New booking on your trip", "{seats} seat(s) booked - {seatsLeft} left.",
            "حجز جديد على رحلتك", "تم حجز {seats} مقعد — بقي {seatsLeft}."),

        [NotificationTemplate.BookingCancelledDriver] = new(
            NotificationType.BookingCancelled,
            "A rider cancelled", "{seats} seat(s) freed on {origin} -> {destination}.",
            "ألغى راكب حجزه", "تحرّر {seats} مقعد على رحلة من {origin} إلى {destination}."),

        [NotificationTemplate.TripCancelledRider] = new(
            NotificationType.TripCancelled,
            "Trip cancelled", "The driver cancelled {origin} -> {destination}.",
            "أُلغيت الرحلة", "ألغى السائق الرحلة من {origin} إلى {destination}."),

        [NotificationTemplate.TripStartedRider] = new(
            NotificationType.TripStarted,
            "Your trip has started", "{origin} -> {destination}",
            "بدأت رحلتك", "من {origin} إلى {destination}"),

        [NotificationTemplate.DriverArrivedRider] = new(
            NotificationType.DriverArrived,
            "Your driver has arrived", "Waiting for you at {origin}.",
            "وصل السائق", "في انتظارك عند {origin}."),

        // Filed as a cancellation rather than under a type of its own: to the
        // rider's inbox the seat is simply gone, and the wording carries why.
        [NotificationTemplate.BookingNoShowRider] = new(
            NotificationType.BookingCancelled,
            "You were marked as a no-show", "The driver waited at {origin} and left without you.",
            "تم تسجيلك كغير حاضر", "انتظرك السائق عند {origin} ثم انطلق بدونك."),

        [NotificationTemplate.TripCompletedRider] = new(
            NotificationType.TripCompleted,
            "Trip completed", "Hope it went well - tap to rate your driver.",
            "انتهت الرحلة", "نأمل أن تكون رحلة موفّقة — اضغط لتقييم السائق."),

        [NotificationTemplate.TripMatchedRider] = new(
            NotificationType.TripMatched,
            "A ride just opened up", "{name} is driving {origin} -> {destination}.",
            "توفّرت رحلة مناسبة", "{name} يقود من {origin} إلى {destination}."),

        [NotificationTemplate.DriverAcceptedRider] = new(
            NotificationType.DriverAccepted,
            "A driver accepted your ride", "{name} is on the way.",
            "قبل سائق رحلتك", "{name} في الطريق إليك."),

        [NotificationTemplate.DriverVerified] = new(
            NotificationType.DriverVerified,
            "You are approved to drive",
            "Your driver application was approved. You can start accepting rides.",
            "تم اعتماد حسابك كسائق",
            "تم اعتماد طلبك كسائق. يمكنك الآن قبول الرحلات."),

        [NotificationTemplate.DriverRejected] = new(
            NotificationType.DriverRejected,
            "Driver application declined",
            "Your driver application was not approved. Contact support if you think this is a mistake.",
            "تم رفض طلب السائق",
            "لم يتم اعتماد طلبك كسائق. تواصل مع الدعم إذا كنت تعتقد أن هناك خطأ."),

        // The reply itself is not in the push. It is one person's answer to one
        // person's complaint, written in free text the desk never expected a
        // lock screen to carry — and it can be long. The notification says an
        // answer arrived; the screen shows what it says.
        [NotificationTemplate.FeedbackReplied] = new(
            NotificationType.FeedbackReplied,
            "Support replied", "We answered \"{subject}\". Tap to read it.",
            "ردّ فريق الدعم", "أجبنا على \"{subject}\". اضغط للقراءة."),

        [NotificationTemplate.RatingReceived] = new(
            NotificationType.RatingReceived,
            "You got a new rating", "{stars} out of 5 for your last trip.",
            "وصلك تقييم جديد", "{stars} من 5 على رحلتك الأخيرة."),
    };

    /// <summary>The inbox type a template files under.</summary>
    public static NotificationType TypeOf(NotificationTemplate template) => Templates[template].Type;

    /// <summary>
    /// Renders both languages. <paramref name="args"/> is any object whose public
    /// properties name the placeholders — typically an anonymous object at the
    /// call site, e.g. <c>new { origin = …, destination = … }</c>.
    /// </summary>
    public static LocalizedText Render(NotificationTemplate template, object? args = null)
    {
        var entry = Templates[template];
        var values = ToValues(args);

        return new LocalizedText
        {
            Title = Fill(entry.TitleEn, values),
            Body = Fill(entry.BodyEn, values),
            TitleAr = Fill(entry.TitleAr, values),
            BodyAr = Fill(entry.BodyAr, values),
        };
    }

    private static Dictionary<string, string> ToValues(object? args)
    {
        if (args == null) return [];
        return args.GetType()
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(args)?.ToString() ?? string.Empty);
    }

    private static string Fill(string template, Dictionary<string, string> values)
    {
        if (values.Count == 0 || !template.Contains('{')) return template;

        var builder = new StringBuilder(template);
        foreach (var (key, value) in values) builder.Replace("{" + key + "}", value);
        return builder.ToString();
    }
}
