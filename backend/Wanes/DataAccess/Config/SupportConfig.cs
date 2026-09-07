using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Support;

namespace Wanes.DataAccess.Config;

public class FaqItemConfig : IEntityTypeConfiguration<FaqItem>
{
    public void Configure(EntityTypeBuilder<FaqItem> b)
    {
        b.Property(x => x.QuestionEn).HasMaxLength(300).IsRequired();
        b.Property(x => x.QuestionAr).HasMaxLength(300).IsRequired();

        // Answers are prose and can run to a few paragraphs, so they stay
        // unbounded rather than guessing a ceiling an admin would later hit.
        b.Property(x => x.AnswerEn).IsRequired();
        b.Property(x => x.AnswerAr).IsRequired();

        // The clients always read the published rows of a category in order;
        // this is exactly that query.
        b.HasIndex(x => new { x.IsPublished, x.Category, x.SortOrder });
    }
}

public class FeedbackConfig : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> b)
    {
        b.Property(x => x.Subject).HasMaxLength(FeedbackRules.MaxSubject).IsRequired();

        // The body and the reply are prose. The length ceiling that matters is
        // the one on the way in (FeedbackRules), not one baked into the column
        // where a later, more generous limit would need a migration.
        b.Property(x => x.Message).IsRequired();

        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not Cascade: the complaint is the record that the platform
        // let someone down. Deleting the trip must not quietly delete the
        // evidence, and the reference is optional anyway.
        b.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);

        // The admin inbox: newest first, filtered by status and kind.
        b.HasIndex(x => new { x.Status, x.Kind, x.Id });

        // "My submissions", newest first.
        b.HasIndex(x => new { x.UserId, x.Id });
    }
}
