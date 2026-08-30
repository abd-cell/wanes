using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminBookingService : IAdminBookingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<Booking> bookingRepository;

    public AdminBookingService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<PageOutput<BookingRow>>> List(PageInput page, BookingStatus? status, int? tripId, int? riderId)
    {
        IQueryable<Booking> query = bookingRepository.Query().Include(b => b.Rider).Include(b => b.Trip);

        if (status != null) query = query.Where(b => b.Status == status);
        if (tripId != null) query = query.Where(b => b.TripId == tripId);
        if (riderId != null) query = query.Where(b => b.RiderId == riderId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.Id).Paginate(page).ToListAsync();

        var rows = items.Select(BuildRow).ToList();

        return new BaseResponse<PageOutput<BookingRow>>(new PageOutput<BookingRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<BookingRow>> Get(int id)
    {
        var booking = await bookingRepository.Query()
            .Include(b => b.Rider)
            .Include(b => b.Trip)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking == null) return new BaseResponse<BookingRow>(default, ErrorCode.NotFound);
        return new BaseResponse<BookingRow>(BuildRow(booking));
    }

    public async Task<BaseResponse<BookingRow>> Create(BookingInput input)
    {
        var booking = new Booking();
        Apply(booking, input);
        bookingRepository.Create(booking);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.bookings.create", nameof(Booking), booking.Id);
        return await Get(booking.Id);
    }

    public async Task<BaseResponse<BookingRow>> Update(int id, BookingInput input)
    {
        var booking = await bookingRepository.GetByIdAsync(id);
        if (booking == null) return new BaseResponse<BookingRow>(default, ErrorCode.NotFound);

        Apply(booking, input);
        bookingRepository.Update(booking);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.bookings.update", nameof(Booking), booking.Id);
        return await Get(booking.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var booking = await bookingRepository.GetByIdAsync(id);
        if (booking == null) return new BaseResponse(ErrorCode.NotFound);

        bookingRepository.SoftDelete(booking);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.bookings.delete", nameof(Booking), id);
        return new BaseResponse();
    }

    private static void Apply(Booking booking, BookingInput input)
    {
        booking.TripId = input.TripId;
        booking.RiderId = input.RiderId;
        booking.Seats = input.Seats;
        booking.Status = input.Status;
        booking.RideRequestId = input.RideRequestId;
    }

    private static BookingRow BuildRow(Booking booking) => new(booking)
    {
        RiderName = AdminLabels.ForUser(booking.Rider),
        TripSummary = AdminLabels.ForTrip(booking.Trip),
    };
}
