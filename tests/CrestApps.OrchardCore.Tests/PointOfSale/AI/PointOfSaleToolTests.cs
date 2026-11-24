using CrestApps.Core;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.AI;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.PointOfSale.AI;

/// <summary>
/// <c>listRegisterShifts</c> refuses a caller who is not signed in or lacks Manage registers, lists a register's
/// shifts, and returns a closed shift's takings with the difference between counted and expected cash and the tips the
/// cashier declared.
/// </summary>
public sealed class PointOfSaleToolTests
{
    private readonly RegisterShift _shift = new()
    {
        ItemId = "shift-1",
        RegisterId = "register-1",
        RegisterName = "Downtown till",
        Currency = "USD",
        Status = RegisterShiftStatus.Closed,
        OpeningFloat = 100m,
        CountedCash = 245m,
        ExpectedCash = 250m,
        Summary = new RegisterShiftSummary { Sales = 3, Total = 180m, Cash = 150m, Card = 30m },
    };

    private readonly Mock<IRegisterShiftStore> _shifts = new();
    private string _registerId;

    public PointOfSaleToolTests()
    {
        _shifts.Setup(store => store.FindByIdAsync("shift-1", It.IsAny<CancellationToken>())).ReturnsAsync(_shift);
        _shifts.Setup(store => store.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((string registerId, int _, int _, CancellationToken _) => _registerId = registerId)
            .ReturnsAsync((1, (IReadOnlyList<RegisterShift>)[_shift]));
    }

    [Fact]
    public async Task ListRegisterShifts_RefusesACallerWhoIsNotSignedIn()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(signedIn: false), []);

        // Assert
        Assert.Equal(ListRegisterShiftsTool.NotAllowedMessage, result);
        Assert.Empty(_shifts.Invocations);
    }

    [Fact]
    public async Task ListRegisterShifts_RefusesACallerWithoutManageRegisters()
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != PosPermissions.ManageRegisters.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), services, []);

        // Assert
        Assert.Equal(ListRegisterShiftsTool.NotAllowedMessage, result);
        Assert.Equal(PosPermissions.ManageRegisters.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_shifts.Invocations);
    }

    [Fact]
    public async Task ListRegisterShifts_ListsARegistersShifts_AndReturnsOneInFull()
    {
        // Act
        var list = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(), new() { ["registerId"] = "register-1" }));
        var shift = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(), new() { ["shiftId"] = "shift-1" }));
        var missing = await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(), new() { ["shiftId"] = "nope" });

        // Assert
        Assert.Equal("register-1", _registerId);
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(250m, shift.GetProperty("expectedCash").GetDecimal());
        Assert.Equal(-5m, shift.GetProperty("difference").GetDecimal());
        Assert.Equal(150m, shift.GetProperty("cash").GetDecimal());
        Assert.Equal("Nothing was found: no shift has the id 'nope'. List the shifts first.", missing);
    }

    [Fact]
    public async Task ListRegisterShifts_ReturnsTheDeclaredTips_AndWhetherPayrollTookThem()
    {
        // Arrange
        _shift.CardTips = 12.5m;
        _shift.CashTips = 20m;

        // Act
        var open = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(), new() { ["shiftId"] = "shift-1" }));

        _shift.Put(new RegisterShiftPayroll { ConsumerIds = ["pay-run-1"] });
        var taken = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListRegisterShiftsTool(), Build(), new() { ["shiftId"] = "shift-1" }));

        // Assert
        Assert.Equal(12.5m, open.GetProperty("cardTips").GetDecimal());
        Assert.Equal(20m, open.GetProperty("cashTips").GetDecimal());
        Assert.False(open.GetProperty("tipsTakenByPayroll").GetBoolean());
        Assert.True(taken.GetProperty("tipsTakenByPayroll").GetBoolean());
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_shifts.Object);

        return services.BuildServiceProvider();
    }
}
