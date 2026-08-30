using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Vehicles;
using Wanes.Areas.Services.Vehicles.Models;
using Wanes.Shareds.Enums;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

public class VehicleServiceTests
{
    [Fact]
    public async Task Adding_first_vehicle_defaults_it_and_marks_driver_pending()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(new User { Id = 1, Phone = "+962790000000", FirstName = "R" });
        var svc = new VehicleService(uow, new FakeSecurityManager(1), new FakeAuditService(),
            uow.Repository<Vehicle>(), uow.Repository<User>());

        var res = await svc.Create(new VehicleInput
        {
            Make = "Kia", Model = "Rio", Plate = "99", SeatCapacity = 4,
        });

        Assert.True(res.Success);
        Assert.True(res.Data!.IsDefault);

        var user = uow.Store<User>().Single();
        Assert.True(user.IsDriver);
        Assert.Equal(DriverStatus.Pending, user.DriverStatus);
    }

    [Fact]
    public async Task Adding_vehicle_with_invalid_capacity_fails()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(new User { Id = 1, Phone = "+962790000000" });
        var svc = new VehicleService(uow, new FakeSecurityManager(1), new FakeAuditService(),
            uow.Repository<Vehicle>(), uow.Repository<User>());

        var res = await svc.Create(new VehicleInput { Make = "X", Model = "Y", Plate = "1", SeatCapacity = 0 });

        Assert.False(res.Success);
    }
}
