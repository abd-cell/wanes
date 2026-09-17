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
        [NotificationTemplate.RiderTripNearbyDriver] = new(
            NotificationType.RiderTripNearby,
            "A rider needs a driver", "{origin} -> {destination}",
            "راكب يبحث عن سائق", "من {origin} إلى {destination}"),

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

        // Says the seat is theirs, not that a decision is owed: a claim confirms
        // the seat outright, exactly as booking a driver's own trip does. The
        // price is in the payload rather than the line — the figure belongs on
        // the seat card, where the currency is formatted and Leave is one tap
        // away.
        [NotificationTemplate.RiderTripClaimedRider] = new(
            NotificationType.DriverAccepted,
            "A driver took your trip", "{name} is driving it — your seat is confirmed.",
            "سائق أخذ رحلتك", "{name} سيقودها — تم تأكيد مقعدك."),

        [NotificationTemplate.TripConfirmedRider] = new(
            NotificationType.TripConfirmed,
            "Your seat is confirmed", "{origin} -> {destination} has the riders it needed.",
            "تم تأكيد مقعدك", "اكتمل عدد الركاب للرحلة من {origin} إلى {destination}."),

        [NotificationTemplate.TripLowSeatsCancelledRider] = new(
            NotificationType.TripNotEnoughRiders,
            "Trip called off", "{origin} -> {destination} did not get enough riders.",
            "أُلغيت الرحلة", "لم يكتمل عدد الركاب للرحلة من {origin} إلى {destination}."),

        [NotificationTemplate.TripConfirmDecisionDriver] = new(
            NotificationType.ConfirmDecision,
            "Run with {seats} seat(s)?", "{origin} -> {destination} is short of {min}. Run it, or call it off.",
            "هل تنطلق بـ {seats} مقعد؟", "الرحلة من {origin} إلى {destination} أقل من {min}. انطلق بها أو ألغِها."),


        [NotificationTemplate.RideRequestJoinedRider] = new(
            NotificationType.RideRequest,
            "Another rider joined", "{origin} -> {destination} now has more passengers looking for a driver.",
            "انضم راكب آخر", "طلب {origin} إلى {destination} صار فيه ركاب أكثر بانتظار سائق."),

        [NotificationTemplate.RideRequestInterestRider] = new(
            NotificationType.RideRequest,
            "A driver is interested", "{name} offered to drive {origin} -> {destination}.",
            "سائق مهتم", "{name} عرض أن يقود من {origin} إلى {destination}."),

        [NotificationTemplate.RideRequestMatchedRider] = new(
            // DriverAccepted, not RideRequest: this is precisely "a driver
            // accepted", and it is the event the rider's waiting screen hands
            // over on. Giving it the generic type would leave that screen
            // watching for something that no longer arrives.
            NotificationType.DriverAccepted,
            "A driver took your request", "{name} is driving it — your seat is confirmed.",
            "سائق أخذ طلبك", "{name} سيقودها — تم تأكيد مقعدك."),

        [NotificationTemplate.RideRequestNotSelectedDriver] = new(
            NotificationType.RideRequest,
            "Another driver was chosen", "{origin} -> {destination} went to someone else this time.",
            "تم اختيار سائق آخر", "طلب {origin} إلى {destination} ذهب لسائق آخر هذه المرة."),

        [NotificationTemplate.RideRequestMatchedGatheringRider] = new(
            NotificationType.DriverAccepted,
            "A driver will take your ride", "{name} runs it once {min} passengers are in. Your seat is held.",
            "سائق سيأخذ رحلتك", "{name} سينطلق عند اكتمال {min} ركاب. مقعدك محجوز."),

        [NotificationTemplate.TripCancelledReopenedRider] = new(
            NotificationType.TripCancelled,
            "Your driver cancelled", "We put {origin} -> {destination} back on the market and are finding you another driver.",
            "ألغى السائق رحلتك", "أعدنا طلب {origin} إلى {destination} إلى السوق ونبحث لك عن سائق آخر."),

        [NotificationTemplate.DemandAlertMatchedDriver] = new(
            NotificationType.DemandAlert,
            "{seats} passengers want your route", "{origin} -> {destination} is ready to fill your car.",
            "{seats} ركاب على طريقك", "طلب {origin} إلى {destination} جاهز ليملأ سيارتك."),

        [NotificationTemplate.ReliabilityWarningDriver] = new(
            NotificationType.Reliability,
            "Cancellations add up", "You have {points} cancellation points. At {limit}, instant requests pause for {days} days.",
            "الإلغاءات تتراكم", "لديك {points} نقاط إلغاء. عند {limit} تتوقف الطلبات الفورية لمدة {days} أيام."),

        [NotificationTemplate.ReliabilitySuspendedDriver] = new(
            NotificationType.Reliability,
            "Instant requests paused", "Too many cancellations. You can still take scheduled trips; instant requests resume {until}.",
            "توقفت الطلبات الفورية", "إلغاءات كثيرة. ما زال بإمكانك أخذ الرحلات المجدولة؛ تعود الطلبات الفورية في {until}."),

        [NotificationTemplate.SafetyIncidentAdmin] = new(
            NotificationType.SafetyIncident,
            "Safety alert: {kind}", "{name} raised a {kind}. Open the safety queue now.",
            "تنبيه سلامة: {kind}", "{name} أرسل {kind}. افتح قائمة السلامة الآن."),

        [NotificationTemplate.OfferChosenDriver] = new(
            NotificationType.DriverAccepted,
            "The riders chose you", "{origin} -> {destination} is yours — check your trips.",
            "اختارك الركاب", "رحلة {origin} إلى {destination} أصبحت لك — راجع رحلاتك."),

        [NotificationTemplate.SeriesOfferRider] = new(
            NotificationType.Series,
            "{name} offers to drive your series", "{days}: {origin} -> {destination} at {price} a seat. Review the offer.",
            "{name} يعرض قيادة رحلاتك المتكررة", "{daysAr}: من {origin} إلى {destination} بسعر {price} للمقعد. راجع العرض."),

        [NotificationTemplate.SeriesAcceptedDriver] = new(
            NotificationType.Series,
            "The series is yours", "{origin} -> {destination} ({days}). {count} upcoming days are in your trips.",
            "الرحلات المتكررة أصبحت لك", "من {origin} إلى {destination} ({daysAr}). أضيفت {count} أيام قادمة إلى رحلاتك."),

        [NotificationTemplate.SeriesNotSelectedDriver] = new(
            NotificationType.Series,
            "Another driver took the series", "{origin} -> {destination} went to someone else.",
            "سائق آخر أخذ الرحلات المتكررة", "رحلات {origin} إلى {destination} ذهبت لسائق آخر."),

        [NotificationTemplate.SeriesStartedRider] = new(
            NotificationType.Series,
            "{name} will drive your series", "{days}: {count} upcoming days are confirmed.",
            "{name} سيقود رحلاتك المتكررة", "{daysAr}: تم تأكيد {count} أيام قادمة."),

        [NotificationTemplate.SeriesDayOpenRider] = new(
            NotificationType.Series,
            "{date} needs another driver", "Your series driver can't make {date}. It is on the board for other drivers.",
            "{dateAr} يحتاج سائقاً آخر", "سائق رحلاتك لا يستطيع يوم {dateAr}. طرحناه لبقية السائقين."),

        [NotificationTemplate.SeriesDayMissedDriver] = new(
            NotificationType.Series,
            "{date} was not added", "You already have a trip then, so {origin} -> {destination} on {date} went to the board.",
            "لم يُضف يوم {dateAr}", "لديك رحلة في ذلك الوقت، لذا طُرحت رحلة {origin} إلى {destination} يوم {dateAr} لبقية السائقين."),

        [NotificationTemplate.SeriesDaySkippedRider] = new(
            NotificationType.Series,
            "No ride on {date}", "{origin} -> {destination} on {date} is cancelled. The rest of your series stands.",
            "لا رحلة يوم {dateAr}", "أُلغيت رحلة {origin} إلى {destination} يوم {dateAr}. بقية رحلاتك المتكررة قائمة."),

        [NotificationTemplate.SeriesEnded] = new(
            NotificationType.Series,
            "{name} ended the series", "{origin} -> {destination} stops after {date}.",
            "{name} أنهى الرحلات المتكررة", "تتوقف رحلات {origin} إلى {destination} بعد {dateAr}."),

        [NotificationTemplate.SeriesWeeklySummary] = new(
            NotificationType.Series,
            "Your week ahead", "{count} rides on {origin} -> {destination} in the next 7 days.",
            "أسبوعك القادم", "{count} رحلات من {origin} إلى {destination} خلال الأيام السبعة القادمة."),

        [NotificationTemplate.SeriesRiderJoinedDriver] = new(
            NotificationType.Series,
            "{name} booked your series", "{seats} seat(s) on {count} upcoming days of {origin} -> {destination}.",
            "{name} حجز رحلاتك المتكررة", "{seats} مقعد في {count} أيام قادمة من {origin} إلى {destination}."),

        [NotificationTemplate.SeriesDayNotBookedRider] = new(
            NotificationType.Series,
            "{date} could not be booked", "{origin} -> {destination} on {date} is full or clashes with another ride.",
            "تعذر حجز يوم {dateAr}", "رحلة {origin} إلى {destination} يوم {dateAr} ممتلئة أو تتعارض مع رحلة أخرى."),

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

        [NotificationTemplate.DriverRejectedWithReason] = new(
            NotificationType.DriverRejected,
            "Driver application declined",
            "{reason}",
            "تم رفض طلب السائق",
            "{reason}"),

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
