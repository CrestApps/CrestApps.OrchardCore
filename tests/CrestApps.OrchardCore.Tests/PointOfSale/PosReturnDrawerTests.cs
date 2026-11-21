using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Api;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Migrations;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.PointOfSale;

/// <summary>
/// The till return's drawer movement, with the register's shifts in a real SQLite store: the refund commits the request's
/// session, which detaches the shift the endpoint read before it.
/// </summary>
public sealed class PosReturnDrawerTests : IAsyncLifetime
{
    private const string RegisterId = "register-a";
    private const string ShiftId = "shift-1";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-pos-return-{Guid.NewGuid():N}.db");
    private readonly Register _register = new() { ItemId = RegisterId, Name = "Till 1", LocationId = "store-a", IsEnabled = true };
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new RegisterIndexProvider(), new RegisterShiftIndexProvider(), new PosDepartmentIndexProvider()], PosConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(PosConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);

            await new PosMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await new RegisterShiftStore(session).CreateAsync(new RegisterShift
            {
                ItemId = ShiftId,
                RegisterId = RegisterId,
                Currency = "USD",
                Status = RegisterShiftStatus.Open,
                OpeningFloat = 100m,
            }, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// The drawer movement Mild (fix verification): the movement was written to the copy of the shift read before the
    /// refund. The refund's commit detached it, so the version check no longer applied, and a shift a manager closed
    /// meanwhile was saved open again, without its count.
    /// </summary>
    [Fact]
    public async Task Return_WhenAManagerClosesTheShiftDuringTheRefund_LeavesTheShiftClosedWithItsCount()
    {
        // Act
        var result = await ReturnAsync(cashBack: 20m, closeShiftDuringRefund: true);

        // Assert: the shift is closed with its count, and the clerk is told the cash isn't in it.
        await using var session = _store.CreateSession();
        var shifts = new RegisterShiftStore(session);
        var shift = await shifts.FindByIdAsync(ShiftId, TestContext.Current.CancellationToken);

        Assert.Null(await shifts.FindOpenAsync(RegisterId, TestContext.Current.CancellationToken));
        Assert.Equal(RegisterShiftStatus.Closed, shift.Status);
        Assert.Equal(100m, shift.CountedCash);
        Assert.Empty(shift.CashMovements);

        var ok = Assert.IsType<Ok<ApiPosReturn>>(result);
        Assert.Equal(20m, ok.Value.CashBack.Amount);
        Assert.Contains(ok.Value.Messages, message => message.Contains("closed", StringComparison.Ordinal));

        // G15 (fix verification): the cash that isn't in any count was told only as text, with incomplete false.
        Assert.True(ok.Value.Incomplete);
    }

    [Fact]
    public async Task Return_RecordsTheCashOnTheShiftAsItIsAfterTheRefund()
    {
        // Act
        var result = await ReturnAsync(cashBack: 20m, closeShiftDuringRefund: false);

        // Assert
        await using var session = _store.CreateSession();
        var shift = await new RegisterShiftStore(session).FindByIdAsync(ShiftId, TestContext.Current.CancellationToken);

        Assert.Equal(RegisterShiftStatus.Open, shift.Status);
        Assert.Equal(-20m, Assert.Single(shift.CashMovements).Amount);
        Assert.False(Assert.IsType<Ok<ApiPosReturn>>(result).Value.Incomplete);
    }

    /// <summary>
    /// The return's flag Mild (fix verification): a return that paid cash while the card refund failed answered 200 with
    /// the failure only as text in <c>messages</c>, which a till can't read.
    /// </summary>
    [Fact]
    public async Task Return_WhenTheCardPartFails_IsFlaggedIncomplete()
    {
        // Act
        var result = await ReturnAsync(cashBack: 20m, closeShiftDuringRefund: false, error: "The refund of $30.00 failed: card_declined");

        // Assert
        var ok = Assert.IsType<Ok<ApiPosReturn>>(result);
        Assert.True(ok.Value.Incomplete);
        Assert.Equal(20m, ok.Value.CashBack.Amount);
    }

    private async Task<object> ReturnAsync(decimal cashBack, bool closeShiftDuringRefund, string error = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var session = _store.CreateSession();

        var order = new Order
        {
            ItemId = "order-1",
            OrderNumber = "1001",
            Status = OrderStatus.Paid,
            Channel = OrderChannels.PointOfSale,
            RegisterId = RegisterId,
            LocationId = "store-a",
            Currency = "USD",
        };
        var refunded = new OrderOperationResult { Refunded = cashBack };

        refunded.ReturnedToBalances[PosConstants.CashTenderKey] = cashBack;

        if (error is not null)
        {
            refunded.Errors.Add(error);
        }

        var operations = new Mock<IOrderOperationsService>();
        operations
            .Setup(service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                if (closeShiftDuringRefund)
                {
                    // A manager closes the shift, counting 100 in the drawer, in their own request.
                    await using var manager = _store.CreateSession();
                    var managerShifts = new RegisterShiftStore(manager);
                    var shift = await managerShifts.FindByIdAsync(ShiftId, cancellationToken);

                    shift.Status = RegisterShiftStatus.Closed;
                    shift.CountedCash = 100m;
                    shift.ClosedUtc = DateTime.UtcNow;
                    await managerShifts.UpdateAsync(shift, cancellationToken);
                    await manager.SaveChangesAsync(cancellationToken);
                }

                // The refund commits the request's session, as the order operations do.
                await session.SaveChangesAsync(cancellationToken);

                return refunded;
            });

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var registers = new Mock<IRegisterStore>();
        registers.Setup(store => store.FindByIdAsync(RegisterId, It.IsAny<CancellationToken>())).ReturnsAsync(_register);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .AddSingleton(registers.Object)
            .AddSingleton<ISession>(session)
            .AddSingleton<IRegisterShiftStore>(new RegisterShiftStore(session))
            .AddSingleton(authorization.Object)
            .AddSingleton(operations.Object)
            .AddSingleton(Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>())
            .AddSingleton(Mock.Of<global::OrchardCore.Settings.ISiteService>())
            .AddSingleton(Mock.Of<global::OrchardCore.Modules.IClock>(clock => clock.UtcNow == DateTime.UtcNow))
            .AddSingleton<PosShiftService>()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "cashier")], "test")),
        };
        var request = new ApiPosReturnRequest { OrderId = order.ItemId, RegisterId = RegisterId };
        var method = typeof(PosApiEndpoints).GetMethod("ReturnAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(ApiPosReturnRequest) ? request
                : parameter.ParameterType == typeof(HttpContext) ? httpContext
                : services.GetRequiredService(parameter.ParameterType))
            .ToArray();

        var task = (Task)method.Invoke(null, arguments);

        await task;

        // The request ends: its session commits.
        await session.SaveChangesAsync(cancellationToken);

        var result = task.GetType().GetProperty("Result").GetValue(task);

        return result.GetType().GetProperty("Result")?.GetValue(result) ?? result;
    }
}
