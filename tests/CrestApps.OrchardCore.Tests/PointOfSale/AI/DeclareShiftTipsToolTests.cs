using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.AI;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.PointOfSale.AI;

/// <summary>
/// <c>declareShiftTips</c> refuses a caller who is not signed in or lacks Manage registers, declares or corrects the tips
/// of a closed shift through the shift service and commits them, keeps the tip it is not given (the service sets both),
/// refuses an open shift, a shift whose tips payroll took, another currency, a negative tip or more than two decimals,
/// and asks for approval because the card tips are paid on the next pay run.
/// </summary>
public sealed class DeclareShiftTipsToolTests
{
    private readonly RegisterShift _shift = new()
    {
        ItemId = "shift-1",
        RegisterId = "register-1",
        RegisterName = "Downtown till",
        Currency = "USD",
        Status = RegisterShiftStatus.Closed,
        OpenedByName = "ana",
        ClosedUtc = new DateTime(2026, 11, 19, 22, 0, 0, DateTimeKind.Utc),
        CardTips = 12.5m,
        CashTips = 20m,
    };

    private readonly Mock<IRegisterShiftStore> _shifts = new();
    private readonly Mock<YesSqlSession> _session = new();

    public DeclareShiftTipsToolTests()
    {
        _shifts.Setup(store => store.FindByIdAsync("shift-1", It.IsAny<CancellationToken>())).ReturnsAsync(_shift);
    }

    [Fact]
    public async Task DeclareShiftTips_RefusesACallerWhoIsNotSignedIn_OrWithoutManageRegisters()
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != PosPermissions.ManageRegisters.Name);

        // Act
        var anonymous = await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), Build(signedIn: false), Arguments(cardTips: 40m));
        var refused = await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), services, Arguments(cardTips: 40m));

        // Assert
        Assert.Equal(PointOfSaleAIToolBase.NotAllowedMessage, anonymous);
        Assert.Equal(PointOfSaleAIToolBase.NotAllowedMessage, refused);
        Assert.Equal(PosPermissions.ManageRegisters.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_shifts.Invocations);
    }

    [Fact]
    public async Task DeclareShiftTips_CorrectsTheCardTips_KeepsTheCashTips_AndCommits()
    {
        // Act
        var answer = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), Build(), Arguments(cardTips: 42.5m)));

        // Assert
        Assert.Equal(42.5m, _shift.CardTips);
        Assert.Equal(20m, _shift.CashTips);
        Assert.Equal(12.5m, answer.GetProperty("before").GetProperty("cardTips").GetDecimal());
        Assert.Equal(42.5m, answer.GetProperty("after").GetProperty("cardTips").GetDecimal());
        Assert.True(answer.GetProperty("changed").GetBoolean());
        _shifts.Verify(store => store.UpdateAsync(_shift, It.IsAny<CancellationToken>()), Times.Once);
        _session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeclareShiftTips_RefusesAnOpenShift_AndTipsPayrollTook()
    {
        // Act
        _shift.Status = RegisterShiftStatus.Open;
        var open = await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), Build(), Arguments(cardTips: 40m));
        _shift.Status = RegisterShiftStatus.Closed;
        _shift.Put(new RegisterShiftPayroll { ConsumerIds = ["pay-run-1"] });
        var taken = await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), Build(), Arguments(cardTips: 40m));

        // Assert
        Assert.Contains("still open", open, StringComparison.Ordinal);
        Assert.Contains("payroll already took", taken, StringComparison.Ordinal);
        Assert.Equal(12.5m, _shift.CardTips);
        _session.Verify(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("EUR", 40, "give the tips in USD")]
    [InlineData("USD", -1, "can't be less than zero")]
    [InlineData("USD", 40.125, "at most two decimals")]
    public async Task DeclareShiftTips_RefusesWhatTheShiftsPageRefuses(string currency, double cardTips, string expected)
    {
        // Arrange
        var arguments = Arguments(cardTips: (decimal)cardTips);
        arguments["currency"] = currency;

        // Act
        var answer = await AgentToolTestContext.InvokeAsync(new DeclareShiftTipsTool(), Build(), arguments);

        // Assert
        Assert.StartsWith("Nothing was changed", answer, StringComparison.Ordinal);
        Assert.Contains(expected, answer, StringComparison.Ordinal);
        Assert.Equal(12.5m, _shift.CardTips);
    }

    [Fact]
    public void DeclareShiftTips_AsksForApproval()
    {
        // Arrange
        var services = new ServiceCollection();
        new ShiftTipsAIToolsStartup(new PassThroughStringLocalizer<ShiftTipsAIToolsStartup>()).ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        // Act
        var tool = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools[DeclareShiftTipsTool.TheName];

        // Assert
        Assert.True(tool.RequiresApproval);
        Assert.Equal("Point of Sale", tool.Category);
        Assert.True(tool.IsSelectable());
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_shifts.Object);
        services.AddSingleton(_session.Object);
        services.AddSingleton(new PosShiftService(
            Mock.Of<IRegisterStore>(),
            _shifts.Object,
            Mock.Of<IOrderStore>(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IDistributedLock>(),
            _session.Object,
            Mock.Of<ISiteService>(),
            Mock.Of<IClock>(),
            []));

        return services.BuildServiceProvider();
    }

    private static Dictionary<string, object> Arguments(decimal? cardTips = null, decimal? cashTips = null)
    {
        var arguments = new Dictionary<string, object>
        {
            ["shiftId"] = "shift-1",
            ["currency"] = "USD",
        };

        if (cardTips is { } card)
        {
            arguments["cardTips"] = card;
        }

        if (cashTips is { } cash)
        {
            arguments["cashTips"] = cash;
        }

        return arguments;
    }
}
