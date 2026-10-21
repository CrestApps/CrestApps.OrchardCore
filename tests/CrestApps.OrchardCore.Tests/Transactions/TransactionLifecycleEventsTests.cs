using System.Security.Claims;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Controllers;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.Tasks;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A module that keeps its own state in step with a transaction (a registration that is unpaid, paid, refunded or
/// waived) needs one event for every change of status, with the status it had before, raised once and only when the
/// status really changed. Before, a console cancel and a failed installment only saved the record, and overdue had no
/// event at all.
/// </summary>
public sealed class TransactionLifecycleEventsTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_RaisesCreatedOnce()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var manager = TransactionManagerFactory.Resolve(new FakeTransactionStore(), events);
        var transaction = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

        transaction.Status = TransactionStatus.Outstanding;
        transaction.Currency = "USD";
        transaction.TotalAmount = 50m;

        // Act
        await manager.CreateAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([transaction.ItemId], events.Created);
        Assert.Empty(events.StatusChanges);
    }

    [Fact]
    public async Task UpdateAsync_WithoutAStatusChange_RaisesNothing()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var manager = TransactionManagerFactory.Resolve(new FakeTransactionStore(CreateOutstanding()), events);
        var transaction = await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        transaction.Events.Add(new TransactionEvent { CreatedUtc = _now, Type = TransactionEventType.Note, Message = "Called the customer." });
        transaction.ReminderCount++;

        // Act
        await manager.UpdateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);
        await manager.UpdateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, events.Count);
    }

    [Fact]
    public async Task UpdateAsync_AfterTheSameTransactionWasLoadedAgain_StillReportsTheChange()
    {
        // Arrange: the session hands back the same instance, already changed, when it is loaded a second time.
        var events = new RecordingTransactionEventHandler();
        var manager = TransactionManagerFactory.Resolve(new FakeTransactionStore(CreateOutstanding()), events);
        var transaction = await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        transaction.Status = TransactionStatus.PartiallyPaid;

        await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        // Act
        await manager.UpdateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid)], events.StatusChanges);
    }

    [Fact]
    public async Task UpdateAsync_WhenAHandlerFails_StillTellsTheOtherHandlersAndKeepsTheChange()
    {
        // Arrange
        var failing = new Mock<ITransactionEventHandler>();
        failing
            .Setup(handler => handler.StatusChangedAsync(It.IsAny<Transaction>(), It.IsAny<TransactionStatus>(), It.IsAny<TransactionStatus>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The registration could not be updated."));

        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var manager = TransactionManagerFactory.Resolve(store, failing.Object, events);
        var transaction = await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        transaction.Status = TransactionStatus.Paid;

        // Act
        await manager.UpdateAsync(transaction, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(events.StatusChanges);
        Assert.Equal(TransactionStatus.Paid, (await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task Cancel_FromTheConsole_RaisesOneStatusChangeAndCanceledWithTheReason()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var controller = CreateAdminController(TransactionManagerFactory.Create(store, events));

        // Act
        var result = await controller.Cancel("transaction-1", "Financial aid approved.");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Canceled)], events.StatusChanges);
        Assert.Equal([("transaction-1", "Financial aid approved.")], events.Canceled);
    }

    [Fact]
    public async Task MarkPaid_FromTheConsole_RaisesOneStatusChange()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var controller = CreateAdminController(TransactionManagerFactory.Create(store, events));

        // Act
        await controller.MarkPaid("transaction-1", note: null);

        // Assert
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Paid)], events.StatusChanges);
        Assert.Empty(events.Canceled);
    }

    [Fact]
    public async Task AddNote_FromTheConsole_RaisesNothing()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var controller = CreateAdminController(TransactionManagerFactory.Create(store, events));

        // Act
        await controller.AddNote("transaction-1", "Called the customer.");

        // Assert
        Assert.Equal(0, events.Count);
    }

    [Fact]
    public async Task OnlineSettlement_RaisesOneStatusChange()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var manager = TransactionManagerFactory.Create(store, events);
        var settlement = CreateSettlementService(manager);
        var transaction = await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        // Act
        var applied = await settlement.ApplyAsync(transaction, [CreateAttempt("attempt-1", 100m)], "session-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(applied);
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.Paid)], events.StatusChanges);
    }

    [Fact]
    public async Task OnlineSettlement_ThatKeepsTheTransactionPartlyPaid_RaisesNothingTheSecondTime()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(CreateOutstanding());
        var manager = TransactionManagerFactory.Create(store, events);
        var settlement = CreateSettlementService(manager);
        var transaction = await manager.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken);

        // Act
        await settlement.ApplyAsync(transaction, [CreateAttempt("attempt-1", 30m)], "session-1", TestContext.Current.CancellationToken);
        await settlement.ApplyAsync(transaction, [CreateAttempt("attempt-2", 30m)], "session-2", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([("transaction-1", TransactionStatus.Outstanding, TransactionStatus.PartiallyPaid)], events.StatusChanges);
    }

    [Fact]
    public async Task RefundToZero_RaisesOneStatusChange()
    {
        // Arrange
        var events = new RecordingTransactionEventHandler();
        var paid = CreateOutstanding();
        paid.AmountPaid = 100m;
        paid.Status = TransactionStatus.Paid;

        var store = new FakeTransactionStore(paid);
        var handler = CreateRefundHandler(TransactionManagerFactory.Create(store, events));

        // Act
        await handler.RefundSucceededAsync(CreateRefund("refund-1", 40m), TestContext.Current.CancellationToken);
        await handler.RefundSucceededAsync(CreateRefund("refund-2", 60m), TestContext.Current.CancellationToken);

        // Assert: the partial refund keeps it paid, the one that brings it to zero refunds it.
        Assert.Equal([("transaction-1", TransactionStatus.Paid, TransactionStatus.Refunded)], events.StatusChanges);
    }

    [Fact]
    public async Task ReminderSweep_RaisesOverdueOncePerTransaction()
    {
        // Arrange
        var overdue = CreateOutstanding();
        overdue.DueUtc = _now.AddDays(-3);

        var dueToday = CreateOutstanding();
        dueToday.ItemId = "transaction-2";
        dueToday.DueUtc = _now.Date;

        var noDueDate = CreateOutstanding();
        noDueDate.ItemId = "transaction-3";

        var events = new RecordingTransactionEventHandler();
        var store = new FakeTransactionStore(overdue, dueToday, noDueDate);

        // Scheduled reminders are off: the transaction is still reported overdue, it is just not chased.
        var services = new ServiceCollection()
            .AddSingleton(SiteServiceFactory.Create(new TransactionReminderSettings { Enabled = false }))
            .AddSingleton<global::OrchardCore.Modules.IClock>(new TestClock(_now))
            .AddSingleton<ITransactionManager>(TransactionManagerFactory.Create(store, events))
            .AddSingleton<ITransactionEventHandler>(events)
            .AddLogging()
            .BuildServiceProvider();

        var task = new TransactionReminderBackgroundTask();

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["transaction-1"], events.Overdue);
        Assert.Equal(_now, (await store.FindByIdAsync("transaction-1", TestContext.Current.CancellationToken)).BecameOverdueUtc);
        Assert.Null((await store.FindByIdAsync("transaction-2", TestContext.Current.CancellationToken)).BecameOverdueUtc);
        Assert.Empty(events.StatusChanges);
    }

    [Fact]
    public void IsNewlyOverdue_ForAPaidOrAlreadyReportedTransaction_IsFalse()
    {
        // Arrange
        var paid = CreateOutstanding();
        paid.DueUtc = _now.AddDays(-3);
        paid.AmountPaid = paid.TotalAmount;
        paid.Status = TransactionStatus.Paid;

        var reported = CreateOutstanding();
        reported.DueUtc = _now.AddDays(-3);
        reported.BecameOverdueUtc = _now.AddDays(-2);

        // Act & Assert
        Assert.False(TransactionReminderBackgroundTask.IsNewlyOverdue(paid, _now));
        Assert.False(TransactionReminderBackgroundTask.IsNewlyOverdue(reported, _now));
    }

    private static Transaction CreateOutstanding()
        => new()
        {
            ItemId = "transaction-1",
            OwnerId = "customer-1",
            Title = "Fall registration",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

    private static PaymentAttempt CreateAttempt(string itemId, decimal amount)
        => new()
        {
            ItemId = itemId,
            SessionId = "session-1",
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = "transaction-1",
            State = PaymentAttemptState.Succeeded,
            Currency = "USD",
            ConfirmedAmount = amount,
        };

    private static PaymentRefund CreateRefund(string itemId, decimal amount)
        => new()
        {
            ItemId = itemId,
            OriginalAttemptId = "attempt-1",
            Currency = "USD",
            RefundGrossAmount = amount,
            Status = RefundStatus.Succeeded,
        };

    private static TransactionSettlementService CreateSettlementService(ITransactionManager manager)
        => new(
            manager,
            new ServiceCollection().BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<TransactionSettlementService>.Instance,
            new PassThroughStringLocalizer<TransactionSettlementService>());

    private static TransactionRefundHandler CreateRefundHandler(ITransactionManager manager)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPaymentAttemptStore>(new InMemoryPaymentAttemptStore(CreateAttempt("attempt-1", 100m)));

        return new TransactionRefundHandler(
            manager,
            services.BuildServiceProvider(),
            new TestClock(_now),
            new PassThroughStringLocalizer<TransactionRefundHandler>());
    }

    private static AdminController CreateAdminController(ITransactionManager manager)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var controller = new AdminController(
            manager,
            TransactionCommandServiceFactory.Create(manager, new TestClock(_now)),
            authorizationService.Object,
            Mock.Of<IUserService>(),
            Mock.Of<IDisplayNameProvider>(),
            new TestClock(_now),
            Mock.Of<INotifier>(),
            Options.Create(new TransactionSourceOptions()),
            new PassThroughHtmlLocalizer<AdminController>(),
            new PassThroughStringLocalizer<AdminController>(),
            new SourceTextLocalizerFactory());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }
}
