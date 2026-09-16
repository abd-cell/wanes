using Microsoft.AspNetCore.Http;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Users.Driver;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Manual driver verification, end to end: the driver uploads their paperwork,
/// an admin looks at it, and a human decides.
///
/// The behaviour worth pinning is what the old shape got wrong. Verification
/// used to be a licence number and two URL columns nothing ever filled, so an
/// application could reach the review queue with no evidence in it at all and
/// the only honest thing an admin could do was approve blind. These tests hold
/// the two ends of that: an application cannot be submitted without the
/// documents a reviewer needs, and a rejection carries a reason back to the
/// driver instead of leaving them to guess which photo to retake.
/// </summary>
public class DriverVerificationTests
{
    private const int DriverId = 7;
    private const int AdminId = 1;

    private readonly List<User> users;
    private readonly List<DriverDocument> documents = [];
    private readonly List<AuditLog> auditLogs = [];

    private readonly FakeFileStorage storage = new();
    private readonly FakeAuditService audit = new();
    private readonly FakeNotificationService notifications = new();
    private readonly FakeSecurityManager security = new(DriverId);

    public DriverVerificationTests()
    {
        users = [Build.Rider(DriverId), Build.Rider(AdminId)];
        users[0].DriverStatus = DriverStatus.None;
        users[0].IsDriver = false;
    }

    private DriverOnboardingService Onboarding()
    {
        var uow = new FakeUnitOfWork();
        return new DriverOnboardingService(uow, security, audit, storage,
            new InMemoryRepository<User>(users), new InMemoryRepository<DriverDocument>(documents));
    }

    private AdminService Admin()
    {
        var uow = new FakeUnitOfWork();
        return new AdminService(uow, audit, notifications, storage, new FakeSecurityManager(AdminId),
            new InMemoryRepository<User>(users), new InMemoryRepository<Vehicle>([]),
            new InMemoryRepository<AuditLog>(auditLogs), new InMemoryRepository<DriverDocument>(documents));
    }

    /// <summary>A one-pixel PNG — real magic bytes, so the signature check passes.</summary>
    private static UploadDriverDocumentInput Png(DriverDocumentType type, string name = "licence.png")
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6];
        return new UploadDriverDocumentInput
        {
            Type = type,
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png",
            },
        };
    }

    private async Task UploadRequiredDocuments()
    {
        var service = Onboarding();
        foreach (var type in DriverDocumentRules.Required)
            Assert.True((await service.UploadDocument(Png(type))).Success);
    }

    [Fact]
    public async Task Upload_stores_the_file_and_describes_it()
    {
        var response = await Onboarding().UploadDocument(Png(DriverDocumentType.LicenseFront, "front.png"));

        Assert.True(response.Success);
        Assert.Equal(DriverDocumentType.LicenseFront, response.Data!.Type);
        Assert.Equal("front.png", response.Data.FileName);
        Assert.False(response.Data.IsPdf);

        // Filed under the owner, and the bytes are somewhere only the storage knows.
        var stored = Assert.Single(documents);
        Assert.Equal(DriverId, stored.UserId);
        Assert.StartsWith($"driver-documents/{DriverId}/", stored.StorageKey);
        Assert.Contains(AuditActions.DriverDocumentUpload, audit.Actions);
    }

    [Fact]
    public async Task Bytes_that_contradict_the_declared_type_are_refused()
    {
        // An executable announcing itself as a PNG. The content type on a
        // multipart part is whatever the client typed there, so it alone proves
        // nothing — this is the check that stops it reaching the disk.
        byte[] bytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        var input = new UploadDriverDocumentInput
        {
            Type = DriverDocumentType.IdDocument,
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "id.png")
            {
                Headers = new HeaderDictionary(),
                ContentType = "image/png",
            },
        };

        var response = await Onboarding().UploadDocument(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.UnsupportedFileType, response.ErrorCode);
        Assert.Empty(documents);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Re_uploading_a_type_replaces_it_rather_than_piling_up()
    {
        var service = Onboarding();
        await service.UploadDocument(Png(DriverDocumentType.LicenseFront, "blurry.png"));
        await service.UploadDocument(Png(DriverDocumentType.LicenseFront, "sharp.png"));

        // A retake is the common case; two live copies would make the reviewer
        // guess which one counts.
        var live = documents.Where(d => !d.IsDeleted).ToList();
        Assert.Equal("sharp.png", Assert.Single(live).FileName);

        // The superseded row survives, so an approval can still be traced to the
        // file that was actually reviewed.
        Assert.Equal(2, documents.Count);
        Assert.Equal(2, storage.Files.Count);
    }

    [Fact]
    public async Task Apply_is_refused_until_every_required_document_is_on_file()
    {
        var service = Onboarding();
        await service.UploadDocument(Png(DriverDocumentType.LicenseFront));

        var response = await service.Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DriverDocumentsIncomplete, response.ErrorCode);
        Assert.Equal(DriverStatus.None, users[0].DriverStatus);

        var status = await service.GetStatus();
        Assert.False(status.Data!.CanSubmit);
        Assert.Contains(DriverDocumentType.IdDocument, status.Data.MissingTypes);
    }

    [Fact]
    public async Task Apply_with_the_full_set_lands_in_the_review_queue()
    {
        await UploadRequiredDocuments();

        var response = await Onboarding().Apply(new DriverApplyInput { LicenseNumber = " JO-99 " });

        Assert.True(response.Success);
        Assert.Equal(DriverStatus.Pending, users[0].DriverStatus);
        Assert.Equal("JO-99", users[0].LicenseNumber);
        Assert.True(users[0].IsDriver);
        Assert.NotNull(users[0].DriverAppliedAt);

        // Nothing here decided anything — a human still has to.
        Assert.DoesNotContain(AuditActions.AdminDriverVerified, audit.Actions);
    }

    [Fact]
    public async Task The_admin_queue_shows_the_application_with_its_documents()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        var page = await Admin().GetPendingDrivers(new PageInput { PageNumber = 1, PageSize = 10 });

        var row = Assert.Single(page.Data!.Data);
        Assert.Equal(DriverId, row.Id);
        Assert.Equal(DriverDocumentRules.Required.Length, row.DocumentCount);
        Assert.NotNull(row.AppliedAt);

        var listed = await Admin().GetDriverDocuments(DriverId);
        Assert.Equal(DriverDocumentRules.Required.Length, listed.Data!.Count);
    }

    [Fact]
    public async Task Opening_a_document_as_an_admin_is_audited()
    {
        await UploadRequiredDocuments();
        var document = documents[0];

        var response = await Admin().OpenDriverDocument(document.Id);

        Assert.True(response.Success);
        Assert.Equal("image/png", response.Data!.ContentType);
        Assert.NotNull(response.Data.Content);

        // Reading someone's identity papers is itself an act to account for.
        Assert.Contains(AuditActions.AdminDriverDocumentViewed, audit.Actions);
    }

    [Fact]
    public async Task One_driver_cannot_open_another_drivers_document()
    {
        await UploadRequiredDocuments();
        var someoneElse = new FakeSecurityManager(AdminId);
        var service = new DriverOnboardingService(new FakeUnitOfWork(), someoneElse, audit, storage,
            new InMemoryRepository<User>(users), new InMemoryRepository<DriverDocument>(documents));

        var response = await service.OpenDocument(documents[0].Id);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DriverDocumentNotFound, response.ErrorCode);
    }

    [Fact]
    public async Task Approval_records_who_decided_and_tells_the_driver()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        var response = await Admin().VerifyDriver(DriverId, new VerifyInput { Approve = true });

        Assert.True(response.Success);
        Assert.Equal(DriverStatus.Verified, users[0].DriverStatus);
        Assert.Equal(AdminId, users[0].DriverReviewedBy);
        Assert.NotNull(users[0].DriverReviewedAt);
        Assert.Contains($"{DriverId}:{NotificationTemplate.DriverVerified}", notifications.Sent);
    }

    [Fact]
    public async Task A_rejection_carries_the_reviewers_reason_back_to_the_driver()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        await Admin().VerifyDriver(DriverId, new VerifyInput
        {
            Approve = false,
            Note = "The back of the licence is out of focus.",
        });

        Assert.Equal(DriverStatus.Rejected, users[0].DriverStatus);
        Assert.Equal("The back of the licence is out of focus.", users[0].DriverReviewNote);

        // Told with the reason, not the generic wording: "declined" on its own
        // is an instruction to guess.
        Assert.Contains($"{DriverId}:{NotificationTemplate.DriverRejectedWithReason}", notifications.Sent);

        var status = await Onboarding().GetStatus();
        Assert.Equal("The back of the licence is out of focus.", status.Data!.ReviewNote);
    }

    [Fact]
    public async Task A_rejection_with_no_note_falls_back_to_the_generic_wording()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        await Admin().VerifyDriver(DriverId, new VerifyInput { Approve = false, Note = "   " });

        // A body reading "Reason: " with nothing after it is worse than saying
        // less.
        Assert.Contains($"{DriverId}:{NotificationTemplate.DriverRejected}", notifications.Sent);
        Assert.Null(users[0].DriverReviewNote);
    }

    [Fact]
    public async Task Re_applying_clears_the_previous_verdict()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });
        await Admin().VerifyDriver(DriverId, new VerifyInput { Approve = false, Note = "Blurry." });

        // The driver retakes the photo and sends it again.
        await Onboarding().UploadDocument(Png(DriverDocumentType.LicenseBack, "sharp.png"));
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });

        Assert.Equal(DriverStatus.Pending, users[0].DriverStatus);

        // Last time's verdict is not the state of this application, and leaving
        // it would keep showing a rejection the driver has already answered.
        Assert.Null(users[0].DriverReviewNote);
        Assert.Null(users[0].DriverReviewedAt);
    }

    [Fact]
    public async Task A_decided_application_is_still_reachable_by_status()
    {
        await UploadRequiredDocuments();
        await Onboarding().Apply(new DriverApplyInput { LicenseNumber = "JO-99" });
        await Admin().VerifyDriver(DriverId, new VerifyInput { Approve = false, Note = "Mis-click." });

        var pending = await Admin().GetPendingDrivers(new PageInput { PageNumber = 1, PageSize = 10 });
        Assert.Empty(pending.Data!.Data);

        // Without this filter a driver rejected by mistake would be unreachable
        // until they re-applied.
        var rejected = await Admin().GetPendingDrivers(
            new PageInput { PageNumber = 1, PageSize = 10 }, DriverStatus.Rejected);
        Assert.Equal(DriverId, Assert.Single(rejected.Data!.Data).Id);
    }
}
