using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Payroll;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Payroll;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The pay inputs Point of Sale and Orders hand to payroll: tips cashiers declare at the close of a shift, and the
/// commission staff earn on the orders they make. Each is taken once, and given back on a void.
/// </summary>
public sealed class PayInputSourcesTests
{
    private static readonly PayCode[] _codes =
    [
        new() { ItemId = "tips", Code = "TIPS", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Tips },
        new() { ItemId = "ctips", Code = "CTIPS", Class = PayCodeClass.ImputedIncome, Kind = PayCodeKinds.Tips },
        new() { ItemId = "comm", Code = "COMM", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Commission },
    ];

    private static readonly PayInputQuery _period = new() { PeriodStart = new DateOnly(2026, 11, 1), PeriodEnd = new DateOnly(2026, 11, 14) };

    [Fact]
    public async Task Tips_OfClosedShifts_InThePeriod_AreTheCashiersTipsAndCashTips_TakenOnce()
    {
        var shifts = new[]
        {
            Shift("s1", "ana-user", new DateTime(2026, 11, 3, 22, 0, 0, DateTimeKind.Utc), card: 42.50m, cash: 10m),
            Shift("s2", "ana-user", new DateTime(2026, 11, 15, 1, 0, 0, DateTimeKind.Utc), card: 5m, cash: null),
            Shift("s3", "nobody-user", new DateTime(2026, 11, 4, 22, 0, 0, DateTimeKind.Utc), card: 7m, cash: null),
            Shift("s4", "ana-user", new DateTime(2026, 11, 5, 22, 0, 0, DateTimeKind.Utc), card: null, cash: null),
        };
        var store = new Mock<IRegisterShiftStore>();

        store.Setup(value => value.ListClosedAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>())).ReturnsAsync(shifts);
        store.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string id, CancellationToken _) => shifts.FirstOrDefault(shift => shift.ItemId == id));

        var source = new PosTipsPayInputSource(store.Object, Staff(), Codes(), LocalClock());

        var inputs = await source.GetInputsAsync(_period, TestContext.Current.CancellationToken);

        // Shift 2 closed after the period, shift 3's cashier is no staff member and shift 4 declared nothing.
        Assert.Equal([("tips", 42.50m), ("ctips", 10m)], inputs.Select(input => (input.PayCodeId, input.Amount.Value)));
        Assert.All(inputs, input => Assert.Equal(("ana", "s1", PosTipsPayInputSource.Kind, new DateOnly(2026, 11, 3)), (input.StaffMemberId, input.SourceId, input.SourceKind, input.Date.Value)));

        await source.MarkConsumedAsync("run-1", ["s1"], TestContext.Current.CancellationToken);

        Assert.True(PosShiftService.TipsTaken(shifts[0]));
        Assert.Empty(await source.GetInputsAsync(_period, TestContext.Current.CancellationToken));
        Assert.All(await source.GetInputsAsync(new PayInputQuery { PeriodStart = _period.PeriodStart, PeriodEnd = _period.PeriodEnd, IncludeConsumed = true }, TestContext.Current.CancellationToken), input => Assert.True(input.Consumed));

        await source.ReleaseAsync("run-1", ["s1"], TestContext.Current.CancellationToken);

        Assert.False(PosShiftService.TipsTaken(shifts[0]));
        Assert.Equal(2, (await source.GetInputsAsync(_period, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Commission_IsTheSellersRate_OfWhatPaidOrdersCameToAfterDiscountsAndRefunds_TakenOnce()
    {
        var orders = new[]
        {
            Order("o1", "ana-user", 200m, discount: 20m, refunded: 30m, new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc)),
            Order("o2", "ana-user", 100m, discount: 0m, refunded: 0m, new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc)),
            Order("o3", "ben-user", 500m, discount: 0m, refunded: 0m, new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc)),
            Order("o4", null, 300m, discount: 0m, refunded: 0m, new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc)),
            Order("o5", "ana-user", 80m, discount: 0m, refunded: 0m, new DateTime(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc), OrderStatus.Canceled),
        };
        var store = new Mock<IOrderStore>();

        store.Setup(value => value.PageAsync(It.IsAny<OrderQuery>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((orders.Length, orders));
        store.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns((string id, CancellationToken _) => ValueTask.FromResult(orders.FirstOrDefault(order => order.ItemId == id)));

        var clock = new Mock<IClock>();

        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));

        var source = new OrderCommissionPayInputSource(new OrderReportReader(store.Object), store.Object, Staff(), Codes(), LocalClock(), clock.Object);

        var inputs = await source.GetInputsAsync(_period, TestContext.Current.CancellationToken);

        // Ana sells at 5%: (200 - 20 - 30) x 5% = 7.50. Order 2 was paid after the period, Ben has no commission rate,
        // order 4 had no seller and order 5 was canceled.
        var commission = Assert.Single(inputs);

        Assert.Equal(("ana", "comm", 7.50m, "o1", OrderCommissionPayInputSource.Kind, new DateOnly(2026, 11, 2)), (commission.StaffMemberId, commission.PayCodeId, commission.Amount.Value, commission.SourceId, commission.SourceKind, commission.Date.Value));

        await source.MarkConsumedAsync("run-1", ["o1"], TestContext.Current.CancellationToken);

        Assert.Empty(await source.GetInputsAsync(_period, TestContext.Current.CancellationToken));
        Assert.True(orders[0].TryGet<OrderCommissionPayroll>(out var taken) && taken.ConsumerIds.Contains("run-1"));

        await source.ReleaseAsync("run-1", ["o1"], TestContext.Current.CancellationToken);

        Assert.Single(await source.GetInputsAsync(_period, TestContext.Current.CancellationToken));
    }

    private static RegisterShift Shift(string id, string userId, DateTime closedUtc, decimal? card, decimal? cash)
        => new()
        {
            ItemId = id,
            OpenedByUserId = userId,
            OpenedUtc = closedUtc.AddHours(-8),
            ClosedUtc = closedUtc,
            Status = RegisterShiftStatus.Closed,
            CardTips = card,
            CashTips = cash,
        };

    private static Order Order(string id, string staffUserId, decimal subtotal, decimal discount, decimal refunded, DateTime paidUtc, OrderStatus status = OrderStatus.Paid)
        => new()
        {
            ItemId = id,
            StaffUserId = staffUserId,
            Subtotal = subtotal,
            Discounts = discount > 0 ? [new OrderDiscount { Amount = discount }] : [],
            Refunds = refunded > 0 ? [new OrderRefund { Amount = refunded }] : [],
            Status = status,
            CreatedUtc = paidUtc.AddMinutes(-5),
            PaidUtc = paidUtc,
        };

    private static IStaffMemberManager Staff()
    {
        var ana = new StaffMember { ItemId = "ana", UserId = "ana-user", DisplayName = "Ana" };
        var ben = new StaffMember { ItemId = "ben", UserId = "ben-user", DisplayName = "Ben" };
        var staff = new Mock<IStaffMemberManager>();

        ana.Put(new StaffPayRates { CommissionRate = 5 });
        staff.Setup(value => value.GetByUserIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) => [.. new[] { ana, ben }.Where(member => ids.Contains(member.UserId))]);

        return staff.Object;
    }

    private static IPayCodeManager Codes()
    {
        var codes = new Mock<IPayCodeManager>();

        codes.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_codes);

        return codes.Object;
    }

    private static ILocalClock LocalClock()
    {
        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).Returns<DateTimeOffset>(Task.FromResult);

        return localClock.Object;
    }
}
