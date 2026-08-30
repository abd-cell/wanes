using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminSavedPlaceService : IAdminSavedPlaceService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<SavedPlace> savedPlaceRepository;

    public AdminSavedPlaceService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<SavedPlace> savedPlaceRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.savedPlaceRepository = savedPlaceRepository;
    }

    public async Task<BaseResponse<PageOutput<SavedPlaceRow>>> List(PageInput page, SavedPlaceLabel? label, int? userId)
    {
        IQueryable<SavedPlace> query = savedPlaceRepository.Query().Include(p => p.User);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Address.Contains(term));
        }
        if (label != null) query = query.Where(p => p.Label == label);
        if (userId != null) query = query.Where(p => p.UserId == userId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.Id).Paginate(page).ToListAsync();

        var rows = items.Select(p => new SavedPlaceRow(p)
        {
            OwnerName = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : null
        }).ToList();

        return new BaseResponse<PageOutput<SavedPlaceRow>>(new PageOutput<SavedPlaceRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<SavedPlaceRow>> Get(int id)
    {
        var place = await savedPlaceRepository.Query().Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (place == null) return new BaseResponse<SavedPlaceRow>(default, ErrorCode.NotFound);
        return new BaseResponse<SavedPlaceRow>(BuildRow(place));
    }

    public async Task<BaseResponse<SavedPlaceRow>> Create(SavedPlaceInput input)
    {
        var place = new SavedPlace { UserId = input.UserId };
        Apply(place, input);
        savedPlaceRepository.Create(place);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.places.create", nameof(SavedPlace), place.Id);
        return new BaseResponse<SavedPlaceRow>(await BuildRowWithOwner(place.Id));
    }

    public async Task<BaseResponse<SavedPlaceRow>> Update(int id, SavedPlaceInput input)
    {
        var place = await savedPlaceRepository.GetByIdAsync(id);
        if (place == null) return new BaseResponse<SavedPlaceRow>(default, ErrorCode.NotFound);

        place.UserId = input.UserId;
        Apply(place, input);
        savedPlaceRepository.Update(place);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.places.update", nameof(SavedPlace), place.Id);
        return new BaseResponse<SavedPlaceRow>(await BuildRowWithOwner(place.Id));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var place = await savedPlaceRepository.GetByIdAsync(id);
        if (place == null) return new BaseResponse(ErrorCode.NotFound);

        savedPlaceRepository.SoftDelete(place);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.places.delete", nameof(SavedPlace), id);
        return new BaseResponse();
    }

    private static void Apply(SavedPlace place, SavedPlaceInput input)
    {
        place.Label = input.Label;
        place.Name = input.Name?.Trim() ?? string.Empty;
        place.Address = input.Address?.Trim() ?? string.Empty;
        place.Location = GeoFactory.Point(input.Lat, input.Lng);
    }

    private static SavedPlaceRow BuildRow(SavedPlace place) => new(place)
    {
        OwnerName = place.User != null ? $"{place.User.FirstName} {place.User.LastName}".Trim() : null
    };

    private async Task<SavedPlaceRow> BuildRowWithOwner(int id)
    {
        var place = await savedPlaceRepository.Query().Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        return BuildRow(place!);
    }
}
