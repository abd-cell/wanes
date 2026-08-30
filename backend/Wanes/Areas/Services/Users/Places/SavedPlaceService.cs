using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Users.Places.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Places;

public class SavedPlaceService : ISavedPlaceService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<SavedPlace> savedPlaceRepository;

    public SavedPlaceService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<SavedPlace> savedPlaceRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.savedPlaceRepository = savedPlaceRepository;
    }

    public async Task<BaseResponse<List<SavedPlaceOutput>>> GetUserPlaces()
    {
        var userId = securityManager.RequireUserId();
        var places = await savedPlaceRepository
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Label).ThenBy(p => p.Name)
            .ToListAsync();

        var data = places.Select(p => new SavedPlaceOutput(p)).ToList();
        return new BaseResponse<List<SavedPlaceOutput>>(data);
    }

    public async Task<BaseResponse<SavedPlaceOutput>> Create(SavedPlaceInput input)
    {
        var userId = securityManager.RequireUserId();
        var place = new SavedPlace
        {
            UserId = userId,
            Label = input.Label,
            Name = input.Name.Trim(),
            Address = input.Place.Address,
            Location = GeoFactory.Point(input.Place.Lat, input.Place.Lng),
        };
        savedPlaceRepository.Create(place);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.PlaceAdd, nameof(SavedPlace), place.Id);
        return new BaseResponse<SavedPlaceOutput>(new SavedPlaceOutput(place));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var userId = securityManager.RequireUserId();
        var place = savedPlaceRepository.FirstOrDefault(p => p.Id == id && p.UserId == userId);
        if (place == null) return new BaseResponse(ErrorCode.NotFound);

        savedPlaceRepository.SoftDelete(place);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.PlaceDelete, nameof(SavedPlace), id);
        return new BaseResponse();
    }
}
