using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Configuration;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;

namespace Wanes.DataAccess.Seeders;

/// <summary>Seeds a default admin account and the platform settings row on first run (idempotent).</summary>
public static class DataSeeder
{
    public const string AdminPhone = "+962790000000";

    public static async Task SeedAsync(DatabaseService db)
    {
        var adminKey = AdminPhone.PhoneKey();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.PhoneKey == adminKey);
        if (admin is null)
        {
            admin = new User
            {
                Phone = AdminPhone,
                PhoneKey = adminKey,
                PhoneVerified = true,
                FirstName = "Wanes",
                LastName = "Admin",
                DisplayName = "Admin",
                IsRider = false,
                IsDriver = false,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var hasAdminRole = await db.UserRoles.AnyAsync(r => r.UserId == admin.Id && r.Role == Roles.Admin);
        if (!hasAdminRole)
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, Role = Roles.Admin });
            await db.SaveChangesAsync();
        }

        // The clients read this before sign-in, so the row has to exist from the
        // first run. Property defaults carry the shipped brand and currency.
        if (!await db.AppConfigurations.AnyAsync())
        {
            db.AppConfigurations.Add(new AppConfiguration());
            await db.SaveChangesAsync();
        }

        await SeedFaqAsync(db);
    }

    /// <summary>
    /// Starter help content, so the app's FAQ screen is never empty on a fresh
    /// install. Seeded only when the table is empty: an admin who curates or
    /// deletes these entries should not find them resurrected on next boot.
    /// </summary>
    private static async Task SeedFaqAsync(DatabaseService db)
    {
        if (await db.FaqItems.AnyAsync()) return;

        db.FaqItems.AddRange(
            new FaqItem
            {
                Category = FaqCategory.General,
                SortOrder = 1,
                QuestionEn = "What is Wanes?",
                QuestionAr = "ما هو ونس؟",
                AnswerEn =
                    "Wanes matches riders with drivers going the same way. Tell us where you are "
                    + "heading and we show trips drivers have already posted. If nothing matches, "
                    + "we open a request and notify nearby drivers so one can pick you up.",
                AnswerAr =
                    "يربط ونس الركاب بالسائقين المتوجهين إلى الطريق نفسه. أخبرنا إلى أين تريد الذهاب "
                    + "وسنعرض لك الرحلات التي نشرها السائقون مسبقًا. وإذا لم تتطابق أي رحلة، نفتح طلبًا "
                    + "وننبّه السائقين القريبين ليقبله أحدهم.",
            },
            new FaqItem
            {
                Category = FaqCategory.General,
                SortOrder = 2,
                QuestionEn = "Does Wanes handle payment?",
                QuestionAr = "هل يتولى ونس عملية الدفع؟",
                AnswerEn =
                    "Not yet. The fare you see on a trip is what the driver is asking, and you settle "
                    + "it directly with them. Wanes does not take payment or a commission.",
                AnswerAr =
                    "ليس بعد. الأجرة الظاهرة على الرحلة هي ما يطلبه السائق، وتقوم بتسويتها معه مباشرة. "
                    + "لا يتولى ونس الدفع ولا يأخذ أي عمولة.",
            },
            new FaqItem
            {
                Category = FaqCategory.Riding,
                SortOrder = 1,
                QuestionEn = "How do I book a seat?",
                QuestionAr = "كيف أحجز مقعدًا؟",
                AnswerEn =
                    "Enter where you are starting from and where you are going, then choose when and "
                    + "how many seats you need. Pick a trip from the results and confirm — the seats "
                    + "are held for you straight away.",
                AnswerAr =
                    "أدخل نقطة الانطلاق والوجهة، ثم اختر الوقت وعدد المقاعد التي تحتاجها. اختر رحلة من "
                    + "النتائج وأكّد الحجز — تُحجز المقاعد لك على الفور.",
            },
            new FaqItem
            {
                Category = FaqCategory.Riding,
                SortOrder = 2,
                QuestionEn = "What happens if no trip matches my search?",
                QuestionAr = "ماذا يحدث إذا لم تتطابق أي رحلة مع بحثي؟",
                AnswerEn =
                    "Wanes opens a ride request and notifies verified drivers near you. The first one "
                    + "to accept becomes your driver, and you will get a notification the moment that "
                    + "happens.",
                AnswerAr =
                    "يفتح ونس طلب رحلة وينبّه السائقين المعتمدين القريبين منك. وأول من يقبل الطلب يصبح "
                    + "سائقك، وستصلك رسالة تنبيه في اللحظة نفسها.",
            },
            new FaqItem
            {
                Category = FaqCategory.Riding,
                SortOrder = 3,
                QuestionEn = "Can I cancel a booking?",
                QuestionAr = "هل يمكنني إلغاء الحجز؟",
                AnswerEn =
                    "Yes, from your trip details before the trip starts. Your seats go back to the "
                    + "trip so another rider can take them. If a driver cancels, every booking on that "
                    + "trip is cancelled and everyone is notified.",
                AnswerAr =
                    "نعم، من تفاصيل رحلتك قبل أن تبدأ. تعود مقاعدك إلى الرحلة ليتمكن راكب آخر من "
                    + "حجزها. وإذا ألغى السائق الرحلة، تُلغى جميع الحجوزات عليها ويُبلَّغ الجميع بذلك.",
            },
            new FaqItem
            {
                Category = FaqCategory.Driving,
                SortOrder = 1,
                QuestionEn = "How do I start driving with Wanes?",
                QuestionAr = "كيف أبدأ القيادة مع ونس؟",
                AnswerEn =
                    "Apply from your profile and add at least one vehicle. Once a vehicle is on your "
                    + "account you can post trips right away — there is no review step for a trip.",
                AnswerAr =
                    "قدّم طلبًا من صفحة حسابك وأضف مركبة واحدة على الأقل. وبمجرد إضافة مركبة إلى حسابك "
                    + "يمكنك نشر الرحلات فورًا — لا توجد مرحلة مراجعة للرحلة.",
            },
            new FaqItem
            {
                Category = FaqCategory.Driving,
                SortOrder = 2,
                QuestionEn = "How many seats can I offer on a trip?",
                QuestionAr = "كم مقعدًا يمكنني تقديمه في الرحلة؟",
                AnswerEn =
                    "Up to the passenger capacity of the vehicle you are driving. You can offer fewer "
                    + "if you want the space, but never more. One trip can carry several riders, and "
                    + "it drops out of search once the last seat goes.",
                AnswerAr =
                    "بحدّ أقصى يساوي سعة المركبة التي تقودها. يمكنك تقديم عدد أقل إن أردت مساحة إضافية، "
                    + "لكن ليس أكثر. ويمكن أن تحمل الرحلة الواحدة عدة ركاب، وتختفي من نتائج البحث بعد "
                    + "حجز آخر مقعد.",
            },
            new FaqItem
            {
                Category = FaqCategory.Driving,
                SortOrder = 3,
                QuestionEn = "Can I edit a trip after posting it?",
                QuestionAr = "هل يمكنني تعديل الرحلة بعد نشرها؟",
                AnswerEn =
                    "Only while it still has no bookings. Once a rider has booked, the details are "
                    + "fixed — cancel the trip instead, and everyone booked on it is notified.",
                AnswerAr =
                    "فقط ما دامت بلا حجوزات. وبعد أن يحجز أحد الركاب تصبح التفاصيل ثابتة — ألغِ الرحلة "
                    + "بدلًا من ذلك، وسيُبلَّغ كل من حجز عليها.",
            },
            new FaqItem
            {
                Category = FaqCategory.Account,
                SortOrder = 1,
                QuestionEn = "Can I be both a rider and a driver?",
                QuestionAr = "هل يمكنني أن أكون راكبًا وسائقًا في الوقت نفسه؟",
                AnswerEn =
                    "Yes. One account does both — switch between riding and driving from your profile "
                    + "whenever you like. Your trips and ratings on both sides stay on the same account.",
                AnswerAr =
                    "نعم. حساب واحد يكفي للأمرين — بدّل بين الركوب والقيادة من صفحة حسابك في أي وقت. "
                    + "وتبقى رحلاتك وتقييماتك على الجانبين في الحساب نفسه.",
            },
            new FaqItem
            {
                Category = FaqCategory.Account,
                SortOrder = 2,
                QuestionEn = "Why do I have to verify my phone number?",
                QuestionAr = "لماذا يجب تأكيد رقم هاتفي؟",
                AnswerEn =
                    "Your phone number is how you sign in and how riders and drivers on a trip reach "
                    + "each other. We send a one-time code to confirm the number belongs to you before "
                    + "you can book or drive.",
                AnswerAr =
                    "رقم هاتفك هو وسيلة تسجيل دخولك، وهو ما يتواصل به الركاب والسائقون في الرحلة. "
                    + "نرسل رمزًا لمرة واحدة للتأكد من أن الرقم يخصّك قبل أن تتمكن من الحجز أو القيادة.",
            },
            new FaqItem
            {
                Category = FaqCategory.Safety,
                SortOrder = 1,
                QuestionEn = "What does the verified driver badge mean?",
                QuestionAr = "ماذا تعني علامة السائق المعتمد؟",
                AnswerEn =
                    "It means our team has checked the driver's details. The badge is also what lets a "
                    + "driver accept ride requests, so anyone who comes to pick you up after a request "
                    + "has been verified.",
                AnswerAr =
                    "تعني أن فريقنا قد راجع بيانات السائق. وهذه العلامة هي أيضًا ما يسمح للسائق بقبول "
                    + "طلبات الرحلات، لذا فإن كل من يأتي لِيُقلّك بعد طلب هو سائق معتمد.",
            },
            new FaqItem
            {
                Category = FaqCategory.Safety,
                SortOrder = 2,
                QuestionEn = "How do ratings work?",
                QuestionAr = "كيف تعمل التقييمات؟",
                AnswerEn =
                    "After a trip finishes both sides rate each other once, from one to five stars with "
                    + "an optional comment. Those ratings build the average shown on a profile and help "
                    + "rank trips in search.",
                AnswerAr =
                    "بعد انتهاء الرحلة يقيّم كل طرف الآخر مرة واحدة، من نجمة إلى خمس نجوم مع تعليق "
                    + "اختياري. وتُبنى من هذه التقييمات المعدّل الظاهر في الملف الشخصي، وتساعد في ترتيب "
                    + "الرحلات في نتائج البحث.",
            });

        await db.SaveChangesAsync();
    }
}
