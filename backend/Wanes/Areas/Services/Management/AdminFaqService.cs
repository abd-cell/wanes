using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// Admin CRUD over the FAQ. The clients read the published subset through
/// <see cref="Support.FaqService"/>.
/// </summary>
public class AdminFaqService : IAdminFaqService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<FaqItem> faqRepository;

    public AdminFaqService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<FaqItem> faqRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.faqRepository = faqRepository;
    }

    public async Task<BaseResponse<PageOutput<FaqRow>>> List(PageInput page, FaqCategory? category, bool? isPublished)
    {
        var query = faqRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            // Questions and answers in both languages: an admin hunting for an
            // entry rarely recalls which language they last typed it in.
            query = query.Where(f =>
                f.QuestionEn.Contains(term) || f.QuestionAr.Contains(term) ||
                f.AnswerEn.Contains(term) || f.AnswerAr.Contains(term));
        }
        if (category != null) query = query.Where(f => f.Category == category);
        if (isPublished != null) query = query.Where(f => f.IsPublished == isPublished);

        var total = await query.CountAsync();

        // The order the clients see, so the admin edits the list as it reads.
        var items = await query
            .OrderBy(f => f.Category)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Paginate(page)
            .ToListAsync();

        var rows = items.Select(f => new FaqRow(f)).ToList();
        return new BaseResponse<PageOutput<FaqRow>>(new PageOutput<FaqRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<FaqRow>> Get(int id)
    {
        var faq = await faqRepository.GetByIdAsync(id);
        if (faq == null) return new BaseResponse<FaqRow>(default, ErrorCode.NotFound);
        return new BaseResponse<FaqRow>(new FaqRow(faq));
    }

    public async Task<BaseResponse<FaqRow>> Create(FaqInput input)
    {
        var faq = new FaqItem();
        Apply(faq, input);

        faqRepository.Create(faq);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.faqs.create", nameof(FaqItem), faq.Id, null, new FaqRow(faq));

        return new BaseResponse<FaqRow>(new FaqRow(faq));
    }

    public async Task<BaseResponse<FaqRow>> Update(int id, FaqInput input)
    {
        var faq = await faqRepository.GetByIdAsync(id);
        if (faq == null) return new BaseResponse<FaqRow>(default, ErrorCode.NotFound);

        // Snapshotted before the edit so the trail shows what the answer used to
        // say — the reason to audit published copy at all.
        var before = new FaqRow(faq);

        Apply(faq, input);
        faqRepository.Update(faq);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.faqs.update", nameof(FaqItem), faq.Id, before, new FaqRow(faq));

        return new BaseResponse<FaqRow>(new FaqRow(faq));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var faq = await faqRepository.GetByIdAsync(id);
        if (faq == null) return new BaseResponse(ErrorCode.NotFound);

        var before = new FaqRow(faq);

        faqRepository.SoftDelete(faq);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.faqs.delete", nameof(FaqItem), id, before, null);

        return new BaseResponse();
    }

    private static void Apply(FaqItem faq, FaqInput input)
    {
        faq.Category = input.Category;
        faq.QuestionEn = input.QuestionEn.Trim();
        faq.QuestionAr = input.QuestionAr.Trim();
        faq.AnswerEn = input.AnswerEn.Trim();
        faq.AnswerAr = input.AnswerAr.Trim();
        faq.SortOrder = input.SortOrder;
        faq.IsPublished = input.IsPublished;
    }
}
