using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Files;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Driver;

/// <summary>
/// The driver's half of verification: upload the paperwork, then submit it.
/// Nothing here approves anything — a human does that from the control panel,
/// which is the whole point of the feature.
/// </summary>
public class DriverOnboardingService : IDriverOnboardingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IFileStorage fileStorage;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<DriverDocument> documentRepository;

    public DriverOnboardingService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IFileStorage fileStorage,
        IRepository<User> userRepository,
        IRepository<DriverDocument> documentRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.fileStorage = fileStorage;
        this.userRepository = userRepository;
        this.documentRepository = documentRepository;
    }

    public async Task<BaseResponse<DriverVerificationOutput>> GetStatus()
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return BaseResponse<DriverVerificationOutput>.Fail(ErrorCode.NotFound);

        var documents = await LiveDocuments(userId);
        return new BaseResponse<DriverVerificationOutput>(Describe(user, documents));
    }

    public async Task<BaseResponse<DriverDocumentOutput>> UploadDocument(UploadDriverDocumentInput input)
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.NotFound);

        var file = input.File;
        if (file == null || file.Length == 0)
            return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.ValidationError, "No file was uploaded.");
        if (!Enum.IsDefined(input.Type))
            return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.ValidationError, "Unknown document type.");
        if (file.Length > DriverDocumentRules.MaxBytes)
            return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.FileTooLarge);
        if (!DriverDocumentRules.IsAllowed(file.ContentType))
            return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.UnsupportedFileType);

        await using var content = file.OpenReadStream();

        // Check the first bytes against the declared type before anything is
        // written: a multipart content type is client-supplied text.
        var head = new byte[12];
        var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
        if (!DriverDocumentRules.MatchesSignature(file.ContentType, head.AsSpan(0, read)))
            return BaseResponse<DriverDocumentOutput>.Fail(ErrorCode.UnsupportedFileType);
        content.Position = 0;

        var key = await fileStorage.SaveAsync(
            $"driver-documents/{userId}", DriverDocumentRules.ExtensionFor(file.ContentType), content);

        // Replace rather than accumulate. The old row is soft-deleted, so an
        // approval can still be traced to the file that was actually reviewed;
        // its bytes stay on disk for the same reason.
        var previous = await documentRepository
            .Where(d => d.UserId == userId && d.Type == input.Type && !d.IsDeleted)
            .ToListAsync();
        foreach (var old in previous) documentRepository.SoftDelete(old);

        var document = new DriverDocument
        {
            UserId = userId,
            Type = input.Type,
            StorageKey = key,
            FileName = Path.GetFileName(file.FileName ?? string.Empty),
            ContentType = file.ContentType,
            SizeBytes = file.Length,
        };
        await documentRepository.AddAsync(document);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DriverDocumentUpload, nameof(DriverDocument), document.Id);

        return new BaseResponse<DriverDocumentOutput>(DriverDocumentOutput.From(document));
    }

    public async Task<BaseResponse> DeleteDocument(int id)
    {
        var userId = securityManager.RequireUserId();
        var document = await documentRepository.FirstOrDefaultAsync(
            d => d.Id == id && d.UserId == userId && !d.IsDeleted);
        if (document == null) return new BaseResponse(ErrorCode.DriverDocumentNotFound);

        documentRepository.SoftDelete(document);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DriverDocumentDelete, nameof(DriverDocument), document.Id);
        return new BaseResponse();
    }

    public async Task<BaseResponse<DocumentFile>> OpenDocument(int id)
    {
        var userId = securityManager.RequireUserId();
        var document = await documentRepository.FirstOrDefaultAsync(
            d => d.Id == id && d.UserId == userId && !d.IsDeleted);
        if (document == null) return BaseResponse<DocumentFile>.Fail(ErrorCode.DriverDocumentNotFound);

        var stream = fileStorage.OpenRead(document.StorageKey);
        if (stream == null) return BaseResponse<DocumentFile>.Fail(ErrorCode.DriverDocumentNotFound);

        return new BaseResponse<DocumentFile>(new DocumentFile
        {
            Content = stream,
            ContentType = document.ContentType,
            FileName = document.FileName,
        });
    }

    public async Task<BaseResponse> Apply(DriverApplyInput input)
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        if (string.IsNullOrWhiteSpace(input.LicenseNumber))
            return new BaseResponse(ErrorCode.ValidationError, "License number is required.");

        // Refuse an application a reviewer could not decide on. Letting it
        // through would only move the same rejection to a human an hour later.
        var documents = await LiveDocuments(userId);
        var missing = MissingTypes(documents);
        if (missing.Count > 0)
            return new BaseResponse(ErrorCode.DriverDocumentsIncomplete,
                $"Missing: {string.Join(", ", missing)}.");

        user.IsDriver = true;
        user.LicenseNumber = input.LicenseNumber.Trim();
        user.DriverStatus = DriverStatus.Pending;
        user.DriverAppliedAt = DateTime.UtcNow;

        // A resubmission is a fresh application: last time's verdict is not the
        // state of this one, and leaving the note behind would keep showing the
        // driver a rejection they have already answered.
        user.DriverReviewNote = null;
        user.DriverReviewedAt = null;
        user.DriverReviewedBy = null;

        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DriverApply, nameof(User), user.Id);
        return new BaseResponse();
    }

    private async Task<List<DriverDocument>> LiveDocuments(int userId) =>
        await documentRepository
            .Where(d => d.UserId == userId && !d.IsDeleted)
            .OrderBy(d => d.Type)
            .ToListAsync();

    private static List<DriverDocumentType> MissingTypes(IEnumerable<DriverDocument> documents)
    {
        var held = documents.Select(d => d.Type).ToHashSet();
        return DriverDocumentRules.Required.Where(t => !held.Contains(t)).ToList();
    }

    private static DriverVerificationOutput Describe(User user, List<DriverDocument> documents) => new()
    {
        Status = user.DriverStatus,
        LicenseNumber = user.LicenseNumber,
        AppliedAt = user.DriverAppliedAt,
        ReviewNote = user.DriverReviewNote,
        ReviewedAt = user.DriverReviewedAt,
        Documents = documents.Select(DriverDocumentOutput.From).ToList(),
        MissingTypes = MissingTypes(documents),
    };
}
