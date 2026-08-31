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
