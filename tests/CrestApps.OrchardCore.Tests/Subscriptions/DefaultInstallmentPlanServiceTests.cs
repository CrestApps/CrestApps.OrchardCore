using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// An installment plan takes real money on a schedule, without the customer present. These cover the whole life of
/// a plan: what a valid plan is, starting the schedule only once the down payment is in, charging each payment once,
/// what happens to a declined card, and handing a payment to the customer when the card cannot be charged.
/// </summary>
public sealed class DefaultInstallmentPlanServiceTests
{
    private const string CustomerId = "customer-1";
    private const string ProviderKey = "card";

    private static readonly DateTime _now = new(2027, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateAsync_CreatesADraftWithItsDownPaymentAndACheckoutForTheCustomer()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();

        // Act
        var result = await service.CreateAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));

        var plan = result.Plan;

        Assert.Equal(InstallmentPlanStatus.Draft, plan.Status);
        Assert.Equal(CustomerId, plan.OwnerId);
        Assert.Equal(7, plan.Payments.Count);
        Assert.Equal(1200m, plan.Payments.Sum(payment => payment.Amount));

        var downPaymentTransaction = Assert.Single(context.Transactions.Transactions);

        Assert.Equal(plan.Payments[0].TransactionId, downPaymentTransaction.ItemId);
        Assert.Equal(TransactionStatus.Outstanding, downPaymentTransaction.Status);
        Assert.Equal(300m, downPaymentTransaction.TotalAmount);
        Assert.Equal(SubscriptionConstants.InstallmentPlans.TransactionSource, downPaymentTransaction.Source);
        Assert.Equal(SubscriptionConstants.InstallmentPlans.ReferenceType, downPaymentTransaction.ReferenceType);
        Assert.Equal(plan.ItemId, downPaymentTransaction.ReferenceId);

        var started = Assert.Single(context.Engine.Started);

        Assert.Equal(TransactionsConstants.ReferenceTypes.Transaction, started.ReferenceType);
        Assert.Equal(downPaymentTransaction.ItemId, started.ReferenceId);
        Assert.Equal(CustomerId, started.OwnerId);
        Assert.Equal(plan.DownPaymentSessionId, (await context.Engine.Sessions.GetByReferenceAsync(started.ReferenceType, started.ReferenceId, cancellationToken: TestContext.Current.CancellationToken)).SessionId);
    }

    [Theory]
    [InlineData(1200, 1200, 6, nameof(CreateInstallmentPlanRequest.DownPaymentAmount))]
    [InlineData(1200, -1, 6, nameof(CreateInstallmentPlanRequest.DownPaymentAmount))]
    [InlineData(1200, 300, 0, nameof(CreateInstallmentPlanRequest.InstallmentCount))]
    [InlineData(0, 300, 6, nameof(CreateInstallmentPlanRequest.TotalAmount))]
    public async Task CreateAsync_RejectsAPlanThatDoesNotAddUp(decimal total, decimal downPayment, int count, string member)
    {
        // Arrange
        var context = new TestContextBuilder();
        var request = CreateRequest();
        request.TotalAmount = total;
        request.DownPaymentAmount = downPayment;
        request.InstallmentCount = count;

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Key == member);
        Assert.Empty(context.Transactions.Transactions);
        Assert.Empty(context.Engine.Started);
    }

    [Fact]
    public async Task CreateAsync_RejectsAFirstPaymentDueToday()
    {
        // Arrange
        var context = new TestContextBuilder();
        var request = CreateRequest();
        request.FirstDueUtc = _now.Date;

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanRequest.FirstDueUtc));
    }

    [Fact]
    public async Task CreateAsync_WithoutAProviderThatCanKeepACard_RefusesThePlan()
    {
        // Arrange
        var context = new TestContextBuilder { HasCardProvider = false };

        // Act
        var result = await context.Build().CreateAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Engine.Started);
    }

    [Fact]
    public async Task CreateAsync_ForANewCustomer_CreatesTheirAccountWithoutAPassword()
    {
        // Arrange
        var context = new TestContextBuilder();
        var request = CreateRequest();
        request.CustomerUserId = null;
        request.NewCustomerName = "Jane Doe";
        request.NewCustomerEmail = "jane@example.com";

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));
        context.UserManager.Verify(manager => manager.CreateAsync(It.Is<IUser>(user => ((User)user).Email == "jane@example.com")), Times.Once);
        Assert.Equal("Jane Doe", result.Plan.CustomerName);
        Assert.Equal("jane@example.com", result.Plan.CustomerEmail);
    }

    [Fact]
    public async Task CreateAsync_ForANewCustomer_GivesThemAUserNameTheSiteAccepts()
    {
        // Arrange
        // Found live: a site that allows only letters and digits in a user name refused the email as one.
        var context = new TestContextBuilder();
        var service = context.Build();
        context.UserManager.Object.Options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        context.UserManager.Setup(manager => manager.FindByNameAsync("janedoe")).ReturnsAsync(new User { UserId = "taken" });

        var request = CreateRequest();
        request.CustomerUserId = null;
        request.NewCustomerName = "Jane Doe";
        request.NewCustomerEmail = "jane.doe@example.com";

        // Act
        var result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));
        context.UserManager.Verify(manager => manager.CreateAsync(It.Is<IUser>(user => user.UserName == "janedoe2")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ForAnEmailAlreadyInUse_AsksForTheExistingCustomer()
    {
        // Arrange
        var context = new TestContextBuilder();
        context.UserManager.Setup(manager => manager.FindByEmailAsync("taken@example.com")).ReturnsAsync(new User { UserId = "someone" });

        var request = CreateRequest();
        request.CustomerUserId = null;
        request.NewCustomerName = "Taken";
        request.NewCustomerEmail = "taken@example.com";

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanRequest.NewCustomerEmail));
        context.UserManager.Verify(manager => manager.CreateAsync(It.IsAny<IUser>()), Times.Never);
    }

    [Fact]
    public async Task ActivateAsync_BeforeTheDownPaymentIsIn_SchedulesNothing()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = (await service.CreateAsync(CreateRequest(), TestContext.Current.CancellationToken)).Plan;

        // Act
        var result = await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(InstallmentPlanStatus.Draft, plan.Status);
        Assert.Single(context.Transactions.Transactions);
    }

    [Fact]
    public async Task ActivateAsync_AfterTheDownPayment_KeepsTheCardAndSchedulesEveryPayment()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateAndPayDownPaymentAsync(context, service);

        // Act
        var result = await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);
        Assert.Equal("pm_saved", plan.PaymentMethod.PaymentMethodReference);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.DownPayment.Status);

        var scheduled = context.Transactions.Transactions.Where(transaction => transaction.ObligationId != "0").OrderBy(transaction => transaction.DueUtc).ToArray();

        Assert.Equal(6, scheduled.Length);
        Assert.All(scheduled, transaction =>
        {
            Assert.Equal(TransactionStatus.Pending, transaction.Status);
            Assert.Equal(150m, transaction.TotalAmount);
            Assert.Equal("Visa •••• 4242", transaction.AutoCollection?.PaymentMethodDescription);
        });
        Assert.Equal(new DateTime(2027, 4, 15), scheduled[0].DueUtc.Value.Date);
        Assert.Equal(new DateTime(2027, 9, 15), scheduled[5].DueUtc.Value.Date);

        // Calling it again changes nothing.
        await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        Assert.Equal(7, context.Transactions.Transactions.Count);
    }

    [Fact]
    public async Task ActivateAsync_WhenTheCardWasNotKept_InvoicesTheScheduleInstead()
    {
        // Arrange
        var context = new TestContextBuilder { KeptCard = null };
        var service = context.Build();
        var plan = await CreateAndPayDownPaymentAsync(context, service);

        // Act
        await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);
        Assert.Equal(InstallmentCollectionMethod.Invoice, plan.CollectionMethod);
        Assert.All(context.Transactions.Transactions.Where(transaction => transaction.ObligationId != "0"), transaction => Assert.Null(transaction.AutoCollection));
    }

    [Fact]
    public async Task ActivateAsync_HandsEachPaymentToTheSavedCardChargeService_OnItsDueDate()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        // Act: the plan's sweep on the first due date.
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert: each payment carries its charge (the card the down payment kept, on its due date, retried on the plan
        // settings' days), and the plan charges nothing itself: the saved card charge service does.
        foreach (var payment in plan.Payments.Where(payment => payment.Number > 0))
        {
            var charge = (await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken)).AutoCharge;

            Assert.Equal((plan.DownPaymentSessionId, (string)null, payment.DueUtc), (charge.SourceSessionId, charge.SavedPaymentMethodId, charge.NextAttemptUtc));
            Assert.Equal([1, 3, 5], charge.RetryDays);
        }

        Assert.Single(context.Engine.Begun);
        Assert.Empty(context.Charges.Charged);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, plan.Payments[1].Status);
    }

    [Fact]
    public async Task ProcessAsync_ForADuePaymentWithoutItsCharge_HandsItToTheSavedCardChargeService()
    {
        // Arrange: a payment of a plan that started before the service charged plans: declined once, retried tomorrow.
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        transaction.AutoCharge = null;
        transaction.Status = TransactionStatus.Failed;
        payment.Status = InstallmentPaymentStatus.Failed;
        payment.ChargeAttempts = 1;
        payment.LastFailureMessage = "Declined.";
        payment.NextChargeAttemptUtc = payment.DueUtc.AddDays(1);
        context.Clock.UtcNow = payment.DueUtc.AddHours(2);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(transaction.AutoCharge);
        Assert.Equal((1, payment.DueUtc.AddDays(1), "Declined.", plan.DownPaymentSessionId), (transaction.AutoCharge.Attempts, transaction.AutoCharge.NextAttemptUtc, transaction.AutoCharge.LastFailure, transaction.AutoCharge.SourceSessionId));
    }

    [Fact]
    public async Task ProcessAsync_ForAPaymentWhoseChargeStaffCanceled_HandsItToTheCustomerWhenDue()
    {
        // Arrange: staff canceled the payment's charge on its transaction (the saved card charge service's CancelAsync).
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        transaction.AutoCharge = null;
        transaction.AutoCollection = null;
        context.Clock.UtcNow = payment.DueUtc.AddHours(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(transaction.AutoCharge);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Equal(InstallmentPaymentStatus.Due, payment.Status);
    }

    [Fact]
    public async Task ADeclinedCharge_IsRecordedWithItsRetry_MarksThePlanPastDue_AndTellsTheCustomer()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        context.Clock.UtcNow = payment.DueUtc.AddHours(1);
        context.TransactionEvents.StatusChanges.Clear();

        // Act: the saved card charge service declined the charge and retries it tomorrow.
        await context.DeclineAsync(transaction, "Your card has insufficient funds.");

        // Assert
        Assert.Equal(InstallmentPaymentStatus.Failed, payment.Status);
        Assert.Equal(1, payment.ChargeAttempts);
        Assert.Equal("Your card has insufficient funds.", payment.LastFailureMessage);
        Assert.Equal(context.Clock.UtcNow.AddDays(1), payment.NextChargeAttemptUtc);
        Assert.Equal(InstallmentPlanStatus.PastDue, plan.Status);
        Assert.Equal(TransactionStatus.Failed, transaction.Status);
        Assert.Equal([(transaction.ItemId, TransactionStatus.Pending, TransactionStatus.Failed)], context.TransactionEvents.StatusChanges);
        context.Notifications.Verify(
            notifications => notifications.SendAsync(It.IsAny<object>(), It.Is<INotificationMessage>(message => message.Subject.Contains("could not take", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnAbandonedCharge_HandsThePaymentToTheCustomer()
    {
        // Arrange
        var context = new TestContextBuilder { Settings = new InstallmentPlanSettings { RetryDays = [1] } };
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        context.Clock.UtcNow = payment.DueUtc.AddHours(1);
        await context.DeclineAsync(transaction, "Declined.");
        context.Clock.UtcNow = context.Clock.UtcNow.AddDays(1).AddMinutes(1);

        // Act: the retry is declined too, and no retry is left.
        await context.DeclineAsync(transaction, "Declined.");

        // Assert
        Assert.Equal(2, payment.ChargeAttempts);
        Assert.Equal(InstallmentPaymentStatus.Failed, payment.Status);
        Assert.Null(payment.NextChargeAttemptUtc);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Null(transaction.AutoCollection);
        Assert.Equal(InstallmentPlanStatus.PastDue, plan.Status);
        context.Notifications.Verify(
            notifications => notifications.SendAsync(It.IsAny<object>(), It.Is<INotificationMessage>(message => message.Subject.Contains("overdue", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()),
            Times.Once);

        // A payment handed to the customer is not given a charge again by the schedule.
        context.Clock.UtcNow = context.Clock.UtcNow.AddDays(10);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(transaction.AutoCharge);
    }

    [Fact]
    public async Task ProcessAsync_ForAnInvoicedPlan_AsksTheCustomerToPayOnTheDueDate()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service, InstallmentCollectionMethod.Invoice);

        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var transaction = await context.Transactions.FindByIdAsync(plan.Payments[1].TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Equal(InstallmentPaymentStatus.Due, plan.Payments[1].Status);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);
        Assert.Single(context.Engine.Begun);

        // Unpaid the next day, the plan is behind.
        context.Clock.UtcNow = context.Clock.UtcNow.AddDays(1);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPlanStatus.PastDue, plan.Status);
    }

    [Fact]
    public async Task ProcessAsync_WhenEveryPaymentIsReceived_CompletesThePlan()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service, InstallmentCollectionMethod.Invoice);

        foreach (var transaction in context.Transactions.Transactions)
        {
            transaction.AmountPaid = transaction.TotalAmount;
            transaction.Status = TransactionStatus.Paid;
        }

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstallmentPlanStatus.Completed, plan.Status);
        Assert.NotNull(plan.CompletedUtc);
        Assert.Equal(0m, plan.AmountOutstanding);
        Assert.Equal(plan.TotalAmount, plan.AmountPaid);
    }

    [Fact]
    public async Task ChargeAsync_ChargesThroughTheSavedCardChargeService_AndReportsWhyItWasDeclined()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[3];

        context.Charges.OnCharge = transaction => context.DeclineAsync(transaction, "Your card was declined.");
        context.Clock.UtcNow = payment.DueUtc.AddHours(1);

        // Act
        var result = await service.ChargeAsync(plan.ItemId, 3, TestContext.Current.CancellationToken);

        // Assert: one of the payment's charges, so its retries go on as scheduled.
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Value == "Your card was declined.");
        Assert.Equal([payment.TransactionId], context.Charges.Charged);
        Assert.Equal(InstallmentPaymentStatus.Failed, payment.Status);
        Assert.Equal(context.Clock.UtcNow.AddDays(1), payment.NextChargeAttemptUtc);
        context.Notifications.Verify(
            notifications => notifications.SendAsync(It.IsAny<object>(), It.Is<INotificationMessage>(message => message.Subject.Contains("could not take", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChargeAsync_WhenAnEarlyChargeIsDeclined_PutsThePaymentBackOnItsDueDate()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];

        context.Charges.OnCharge = transaction => context.DeclineAsync(transaction, "Your card was declined.");

        // Act
        var result = await service.ChargeAsync(plan.ItemId, payment.Number, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, payment.Status);
        Assert.Equal("Your card was declined.", payment.LastFailureMessage);
        Assert.Equal(0, payment.ChargeAttempts);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);

        var rescheduled = context.Charges.Scheduled.Last();
        var charge = (await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken)).AutoCharge;

        Assert.Equal((payment.TransactionId, payment.DueUtc, plan.DownPaymentSessionId), (rescheduled.TransactionId, rescheduled.ChargeOnUtc, rescheduled.SourceSessionId));
        Assert.Equal([1, 3, 5], rescheduled.RetryDays);
        Assert.Equal((payment.DueUtc, 0), (charge.NextAttemptUtc, charge.Attempts));
    }

    [Fact]
    public async Task ChargeAsync_BeforeTheDueDate_CollectsEarly()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        // Act
        var result = await service.ChargeAsync(plan.ItemId, 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));
        Assert.Equal([plan.Payments[2].TransactionId], context.Charges.Charged);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.Payments[2].Status);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, plan.Payments[1].Status);
    }

    [Fact]
    public async Task ChargeAsync_BeforeTheDueDate_ForAPaymentHandedToTheCustomer_LeavesItWithTheCustomerWhenDeclined()
    {
        // Arrange: staff canceled the payment's charge before its due date, so the customer pays it.
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        transaction.AutoCharge = null;
        transaction.AutoCollection = null;
        transaction.Status = TransactionStatus.Outstanding;
        context.Charges.OnCharge = charged => context.DeclineAsync(charged, "Your card was declined.");

        // Act
        var result = await service.ChargeAsync(plan.ItemId, payment.Number, TestContext.Current.CancellationToken);

        // Assert: charged once, and not put back on the card's schedule.
        Assert.False(result.Succeeded);
        Assert.Empty(context.Charges.Scheduled);
        Assert.Null(transaction.AutoCharge);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Equal(InstallmentPaymentStatus.Failed, payment.Status);
    }

    [Fact]
    public async Task ChargeAsync_ForAPaymentHandedToTheCustomer_ChargesItOnceWithoutRetries()
    {
        // Arrange: the payment's charges ran out and it was handed to the customer.
        var context = new TestContextBuilder { Settings = new InstallmentPlanSettings { RetryDays = [] } };
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        context.Clock.UtcNow = payment.DueUtc.AddHours(1);
        await context.DeclineAsync(transaction, "Declined.");

        int[] retryDays = null;

        context.Charges.OnCharge = charged =>
        {
            retryDays = charged.AutoCharge?.RetryDays;

            return context.PayAsync(charged);
        };

        // Act
        var result = await service.ChargeAsync(plan.ItemId, payment.Number, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal([], retryDays);
        Assert.Equal(InstallmentPaymentStatus.Paid, payment.Status);
    }

    [Fact]
    public async Task CancelAsync_CancelsWhatIsNotYetReceivedAndKeepsWhatIs()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        // Act
        var result = await service.CancelAsync(plan.ItemId, "Customer request", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(InstallmentPlanStatus.Canceled, plan.Status);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.DownPayment.Status);
        Assert.All(plan.Payments.Where(payment => payment.Number > 0), payment => Assert.Equal(InstallmentPaymentStatus.Canceled, payment.Status));
        Assert.All(
            context.Transactions.Transactions.Where(transaction => transaction.ObligationId != "0"),
            transaction => Assert.Equal(TransactionStatus.Canceled, transaction.Status));
        Assert.Equal(TransactionStatus.Paid, context.Transactions.Transactions.Single(transaction => transaction.ObligationId == "0").Status);

        // A canceled plan is never charged again.
        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddDays(1);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(context.Engine.Begun);
    }

    [Fact]
    public async Task CancelAsync_ForAPlanThatStarted_TellsTheCustomer()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        // Act
        await service.CancelAsync(plan.ItemId, "Customer asked to stop.", TestContext.Current.CancellationToken);

        // Assert
        context.Notifications.Verify(
            notifications => notifications.SendAsync(It.IsAny<object>(), It.Is<INotificationMessage>(message => message.Subject.Contains("canceled", StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ForADraft_TellsTheCustomerNothing()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var created = await service.CreateAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Act
        await service.CancelAsync(created.Plan.ItemId, null, TestContext.Current.CancellationToken);

        // Assert
        context.Notifications.Verify(
            notifications => notifications.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelAsync_ForADraft_ClosesTheDownPaymentCheckout()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = (await service.CreateAsync(CreateRequest(), TestContext.Current.CancellationToken)).Plan;

        // Act
        await service.CancelAsync(plan.ItemId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(plan.DownPaymentSessionId, context.Engine.Canceled);
        Assert.Equal(TransactionStatus.Canceled, context.Transactions.Transactions.Single().Status);
    }

    [Fact]
    public async Task PaymentHandler_ProcessesOnlyThePlansOwnTransactions()
    {
        // Arrange
        var planService = new Mock<IInstallmentPlanService>();
        var handler = new InstallmentPlanTransactionPaymentHandler(planService.Object);

        // Act
        await handler.PaymentRecordedAsync(
            new TransactionPaymentRecordedContext(
                new Transaction { ReferenceType = SubscriptionConstants.InstallmentPlans.ReferenceType, ReferenceId = "plan-1" },
                new TransactionEvent()),
            TestContext.Current.CancellationToken);

        await handler.PaymentRecordedAsync(
            new TransactionPaymentRecordedContext(
                new Transaction { ReferenceType = "SubscriptionPlan", ReferenceId = "plan-2" },
                new TransactionEvent()),
            TestContext.Current.CancellationToken);

        // Assert
        planService.Verify(service => service.ProcessAsync("plan-1", false, It.IsAny<CancellationToken>()), Times.Once);
        planService.Verify(service => service.ProcessAsync("plan-2", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithAReference_CopiesItOntoThePlanAndEveryPayment()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var request = CreateRequest();
        request.ReferenceType = "Registration";
        request.ReferenceId = "reg-1";
        request.Properties = new Dictionary<string, object>
        {
            ["season"] = "2027",
        };

        // Act
        var plan = await CreateActiveAsync(context, service, request);

        // Assert
        Assert.Equal("Registration", plan.ReferenceType);
        Assert.Equal("reg-1", plan.ReferenceId);
        Assert.Equal("2027", plan.Properties["season"]);

        Assert.Equal(7, context.Transactions.Transactions.Count);
        Assert.All(context.Transactions.Transactions, transaction =>
        {
            Assert.Equal("Registration", transaction.ParentReferenceType);
            Assert.Equal("reg-1", transaction.ParentReferenceId);
            Assert.Equal("2027", transaction.Properties["season"]);
        });
    }

    [Fact]
    public async Task GetByParentReference_FromAnyPlanPayment_FindsTheRecordAndThePlan()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var request = CreateRequest();
        request.ReferenceType = "Registration";
        request.ReferenceId = "reg-1";

        var plan = await CreateActiveAsync(context, service, request);

        // Act
        var payments = await context.Transactions.GetByParentReferenceAsync("Registration", "reg-1", TestContext.Current.CancellationToken);
        var lastPayment = payments.Single(transaction => transaction.ObligationId == "6");
        var plans = await context.Plans.GetByReferenceAsync(lastPayment.ParentReferenceType, lastPayment.ParentReferenceId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(7, payments.Count);
        Assert.Same(plan, Assert.Single(plans));
        Assert.Empty(await context.Transactions.GetByParentReferenceAsync("Registration", "reg-2", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Registration", null)]
    [InlineData(null, "reg-1")]
    public async Task CreateAsync_WithHalfAReference_IsRefused(string referenceType, string referenceId)
    {
        // Arrange
        var context = new TestContextBuilder();
        var request = CreateRequest();
        request.ReferenceType = referenceType;
        request.ReferenceId = referenceId;

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanRequest.ReferenceType));
        Assert.Empty(context.Transactions.Transactions);
    }

    [Fact]
    public async Task CreateAsync_WithoutADownPayment_TakesTheFirstPaymentNowAndSchedulesTheRestAfterIt()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var request = CreateRequest();
        request.DownPaymentAmount = 0m;
        request.FirstDueUtc = default;

        // Act
        var result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));

        var plan = result.Plan;

        Assert.Null(plan.DownPayment);
        Assert.Equal(6, plan.Payments.Count);
        Assert.Equal(1200m, plan.Payments.Sum(payment => payment.Amount));
        Assert.Equal(1, plan.FirstPayment.Number);
        Assert.Equal(200m, plan.FirstPayment.Amount);
        Assert.Equal(_now, plan.FirstPayment.DueUtc);
        Assert.Equal(InstallmentPaymentStatus.Due, plan.FirstPayment.Status);

        var firstTransaction = Assert.Single(context.Transactions.Transactions);

        Assert.Equal(plan.FirstPayment.TransactionId, firstTransaction.ItemId);
        Assert.Equal(TransactionStatus.Outstanding, firstTransaction.Status);
        Assert.Equal(200m, firstTransaction.TotalAmount);
        Assert.Equal(firstTransaction.ItemId, Assert.Single(context.Engine.Started).ReferenceId);

        // Once the first payment is in, the card is kept and the remaining payments are scheduled a month apart.
        await context.Engine.BeginPaymentAsync(plan.DownPaymentSessionId, new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await context.Engine.TryCompleteAsync(plan.DownPaymentSessionId, TestContext.Current.CancellationToken);
        await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);
        Assert.Equal("pm_saved", plan.PaymentMethod.PaymentMethodReference);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.FirstPayment.Status);
        Assert.Equal(6, context.Transactions.Transactions.Count);
        Assert.Equal(_now.AddMonths(1), plan.Payments.Single(payment => payment.Number == 2).DueUtc);
        Assert.All(plan.Payments.Where(payment => payment.Number > 1), payment => Assert.Equal(InstallmentPaymentStatus.Scheduled, payment.Status));
    }

    [Fact]
    public async Task CreateAsync_WithoutADownPayment_RefusesAFirstPaymentDueLater()
    {
        // Arrange
        var context = new TestContextBuilder();
        var request = CreateRequest();
        request.DownPaymentAmount = 0m;

        // Act
        var result = await context.Build().CreateAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanRequest.FirstDueUtc));
        Assert.Empty(context.Engine.Started);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_MovesTheCustomersCheckoutOntoTheFirstPaymentAndKeepsTheBalanceOwed()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);

        // Act
        var result = await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));

        var plan = result.Plan;

        Assert.Equal(InstallmentPlanStatus.Draft, plan.Status);
        Assert.Equal(CustomerId, plan.OwnerId);
        Assert.Equal(transaction.ItemId, plan.SourceTransactionId);
        Assert.Equal("Registration", plan.ReferenceType);
        Assert.Equal("reg-1", plan.ReferenceId);
        Assert.Equal("2027", plan.Properties["season"]);
        Assert.Equal(InstallmentCollectionMethod.AutoCharge, plan.CollectionMethod);
        Assert.Equal(0m, plan.DownPaymentAmount);
        Assert.Equal(new[] { 100m, 100m, 100m }, plan.Payments.Select(payment => payment.Amount));
        Assert.Equal(checkout.SessionId, plan.DownPaymentSessionId);

        // The customer's own checkout takes the first payment; no other checkout is started.
        Assert.Empty(context.Engine.Started);

        var firstTransaction = await context.Transactions.FindByIdAsync(plan.FirstPayment.TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Pending, firstTransaction.Status);
        Assert.Equal(100m, firstTransaction.TotalAmount);
        Assert.Equal("reg-1", firstTransaction.ParentReferenceId);
        Assert.Equal(firstTransaction.ItemId, checkout.ReferenceId);
        Assert.True(checkout.SavePaymentMethod);
        Assert.True(checkout.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(100m, invoice.DueNow);

        // Until the first payment is received, nothing changes for the balance.
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
    }

    [Fact]
    public async Task CreateScheduleAsync_TakesAPaymentDueAtTheStartOfTodayInTheSitesZone_ButNotOneDaysAgo()
    {
        // Arrange: late in the evening in the site's zone it is already tomorrow in UTC, so the start of the site's
        // today is the day before in UTC.
        var context = new TestContextBuilder();
        var service = context.Build();
        var balance = new Transaction
        {
            ItemId = "tx-invoice",
            Title = "Invoice INV-1002",
            OwnerId = "invoice-2",
            OwnerKind = CustomerOwnerKind.Guest,
            ReferenceType = "Invoice",
            ReferenceId = "invoice-2",
            Currency = "USD",
            Amount = 200m,
            TotalAmount = 200m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };
        await context.Transactions.CreateAsync(balance, TestContext.Current.CancellationToken);

        // Act
        var daysAgo = await service.CreateScheduleAsync(new CreateInstallmentScheduleRequest
        {
            TransactionId = balance.ItemId,
            Payments = [new() { DueUtc = _now.AddDays(-2), Amount = 100m }, new() { DueUtc = _now.AddDays(30), Amount = 100m }],
        }, TestContext.Current.CancellationToken);
        var startOfToday = await service.CreateScheduleAsync(new CreateInstallmentScheduleRequest
        {
            TransactionId = balance.ItemId,
            Payments = [new() { DueUtc = _now.AddHours(-20), Amount = 100m }, new() { DueUtc = _now.AddDays(30), Amount = 100m }],
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(daysAgo.Succeeded);
        Assert.True(startOfToday.Succeeded, string.Join(" ", startOfToday.Errors.Select(error => error.Value)));
    }

    [Fact]
    public async Task CreateScheduleAsync_SplitsAGuestsBalanceIntoDatedPayments_AndReplacesIt()
    {
        // Arrange: a $300 balance a guest owes for an account, split $100 in ten days and $200 in forty.
        var context = new TestContextBuilder();
        var service = context.Build();
        var balance = new Transaction
        {
            ItemId = "tx-invoice",
            Title = "Invoice INV-1001",
            OwnerId = "invoice-1",
            OwnerKind = CustomerOwnerKind.Guest,
            GuestContactName = "Acme Ltd.",
            GuestContactEmail = "ap@acme.example",
            ReferenceType = "Invoice",
            ReferenceId = "invoice-1",
            Currency = "USD",
            Amount = 300m,
            TotalAmount = 300m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
        };
        balance.SetPayer(new CrestApps.OrchardCore.Parties.Models.PartyRef("Account", "acme"));
        await context.Transactions.CreateAsync(balance, TestContext.Current.CancellationToken);

        // Act
        var wrongTotal = await service.CreateScheduleAsync(new CreateInstallmentScheduleRequest
        {
            TransactionId = balance.ItemId,
            Payments = [new() { DueUtc = _now.AddDays(10), Amount = 100m }, new() { DueUtc = _now.AddDays(40), Amount = 150m }],
        }, TestContext.Current.CancellationToken);
        var outOfOrder = await service.CreateScheduleAsync(new CreateInstallmentScheduleRequest
        {
            TransactionId = balance.ItemId,
            Payments = [new() { DueUtc = _now.AddDays(40), Amount = 100m }, new() { DueUtc = _now.AddDays(10), Amount = 200m }],
        }, TestContext.Current.CancellationToken);
        var created = await service.CreateScheduleAsync(new CreateInstallmentScheduleRequest
        {
            TransactionId = balance.ItemId,
            Payments = [new() { DueUtc = _now.AddDays(10), Amount = 100m }, new() { DueUtc = _now.AddDays(40), Amount = 200m }],
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(wrongTotal.Succeeded);
        Assert.False(outOfOrder.Succeeded);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Value)));

        var plan = created.Plan;
        Assert.Equal((InstallmentPlanStatus.Active, InstallmentCollectionMethod.Invoice, "Invoice", "invoice-1"), (plan.Status, plan.CollectionMethod, plan.ReferenceType, plan.ReferenceId));
        Assert.Equal([(1, 100m), (2, 200m)], plan.Payments.Select(payment => (payment.Number, payment.Amount)));

        var second = await context.Transactions.FindByIdAsync(plan.Payments[1].TransactionId, TestContext.Current.CancellationToken);
        Assert.Equal((TransactionStatus.Outstanding, CustomerOwnerKind.Guest, "ap@acme.example", "Invoice", "invoice-1"), (second.Status, second.OwnerKind, second.GuestContactEmail, second.ParentReferenceType, second.ParentReferenceId));
        Assert.Equal("acme", second.GetPayer().Id);
        Assert.Equal(_now.AddDays(40), second.DueUtc);
        Assert.Equal(TransactionStatus.Canceled, (await context.Transactions.FindByIdAsync(balance.ItemId, TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_AfterTheFirstPayment_KeepsTheCardReplacesTheBalanceAndChargesTheRestAutomatically()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);
        var plan = (await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken)).Plan;

        // Act
        await context.Engine.BeginPaymentAsync(checkout.SessionId, new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await context.Engine.TryCompleteAsync(checkout.SessionId, TestContext.Current.CancellationToken);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);
        Assert.Equal("pm_saved", plan.PaymentMethod.PaymentMethodReference);
        Assert.Equal(TransactionStatus.Canceled, transaction.Status);

        var payments = await context.Transactions.GetByParentReferenceAsync("Registration", "reg-1", TestContext.Current.CancellationToken);

        Assert.Equal(3, payments.Count);
        Assert.All(
            payments.Where(payment => payment.ItemId != plan.FirstPayment.TransactionId),
            payment => Assert.Equal("Visa •••• 4242", payment.AutoCollection?.PaymentMethodDescription));

        // Each later payment is charged on its due date to the card the customer's checkout kept, by the saved card
        // charge service.
        var charge = (await context.Transactions.FindByIdAsync(plan.Payments[1].TransactionId, TestContext.Current.CancellationToken)).AutoCharge;

        Assert.Equal((checkout.SessionId, plan.Payments[1].DueUtc), (charge.SourceSessionId, charge.NextAttemptUtc));
        Assert.Equal(InstallmentPaymentStatus.Scheduled, plan.Payments[1].Status);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_WithADownPaymentOption_SchedulesTheFirstPaymentAPeriodLater()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);
        var request = ConvertRequest(transaction, checkout);
        request.Option.DownPaymentAmount = 60m;

        // Act
        var result = await service.CreateFromTransactionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));
        Assert.Equal(60m, result.Plan.DownPayment.Amount);
        Assert.Equal(new[] { 80m, 80m, 80m }, result.Plan.Payments.Where(payment => payment.Number > 0).Select(payment => payment.Amount));
        Assert.Equal(_now.Date.AddMonths(1), result.Plan.Payments.Single(payment => payment.Number == 1).DueUtc);

        Assert.True(checkout.TryGet<CheckoutInvoice>(out var invoice));
        Assert.Equal(60m, invoice.DueNow);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_WhenTheCustomerLeavesTheCheckout_ClosesThePlanAndLeavesTheBalanceOwed()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);
        var plan = (await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken)).Plan;

        await context.Engine.CancelAsync(checkout.SessionId, "Abandoned.", TestContext.Current.CancellationToken);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstallmentPlanStatus.Canceled, plan.Status);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);

        var firstTransaction = await context.Transactions.FindByIdAsync(plan.FirstPayment.TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(TransactionStatus.Canceled, firstTransaction.Status);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_WhenTheBalanceIsPaidAnotherWay_ClosesThePlanAndItsCheckout()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);
        var plan = (await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken)).Plan;

        transaction.AmountPaid = transaction.TotalAmount;
        transaction.Status = TransactionStatus.Paid;

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstallmentPlanStatus.Canceled, plan.Status);
        Assert.Contains(checkout.SessionId, context.Engine.Canceled);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_ForABalanceAlreadyPaidInInstallments_IsRefused()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);
        var plan = (await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken)).Plan;

        plan.Status = InstallmentPlanStatus.Active;

        var secondCheckout = SeedSettlementCheckout(context, transaction, "session-second");

        // Act
        var result = await service.CreateFromTransactionAsync(ConvertRequest(transaction, secondCheckout), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanFromTransactionRequest.TransactionId));
        Assert.Equal(transaction.ItemId, secondCheckout.ReferenceId);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_AgainAfterALeftCheckout_ClosesTheEarlierPlan()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var firstCheckout = SeedSettlementCheckout(context, transaction);
        var abandoned = (await service.CreateFromTransactionAsync(ConvertRequest(transaction, firstCheckout), TestContext.Current.CancellationToken)).Plan;
        var secondCheckout = SeedSettlementCheckout(context, transaction, "session-second");

        // Act
        var result = await service.CreateFromTransactionAsync(ConvertRequest(transaction, secondCheckout), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));
        Assert.Equal(InstallmentPlanStatus.Canceled, abandoned.Status);
        Assert.Contains(firstCheckout.SessionId, context.Engine.Canceled);
        Assert.Equal(InstallmentPlanStatus.Draft, result.Plan.Status);
    }

    [Theory]
    [InlineData("guest")]
    [InlineData("partly-paid")]
    [InlineData("plan-payment")]
    [InlineData("recurring")]
    public async Task CreateFromTransactionAsync_ForABalanceThatCannotBeSplit_IsRefused(string kind)
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);

        switch (kind)
        {
            case "guest":
                transaction.OwnerKind = CustomerOwnerKind.Guest;
                break;
            case "partly-paid":
                transaction.AmountPaid = 50m;
                break;
            case "plan-payment":
                transaction.ReferenceType = SubscriptionConstants.InstallmentPlans.ReferenceType;
                break;
            case "recurring":
                transaction.Recurrence = new TransactionRecurrence();
                break;
        }

        var checkout = SeedSettlementCheckout(context, transaction);

        // Act
        var result = await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(context.Plans.Plans);
        Assert.Equal(transaction.ItemId, checkout.ReferenceId);
        Assert.False(checkout.SavePaymentMethod);
    }

    [Fact]
    public async Task CreateFromTransactionAsync_AfterAPaymentWasBegun_IsRefused()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var transaction = await SeedSeasonFeeAsync(context);
        var checkout = SeedSettlementCheckout(context, transaction);

        await context.Attempts.CreateAsync(new PaymentAttempt
        {
            ItemId = "attempt-begun",
            SessionId = checkout.SessionId,
            ProviderKey = ProviderKey,
            State = PaymentAttemptState.Pending,
        }, TestContext.Current.CancellationToken);

        // Act
        var result = await service.CreateFromTransactionAsync(ConvertRequest(transaction, checkout), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.Key == nameof(CreateInstallmentPlanFromTransactionRequest.CheckoutSessionId));
        Assert.Empty(context.Plans.Plans);
    }

    private static async Task<InstallmentPlan> CreateActiveAsync(TestContextBuilder context, DefaultInstallmentPlanService service, CreateInstallmentPlanRequest request)
    {
        var result = await service.CreateAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Value)));

        await context.Engine.BeginPaymentAsync(result.Plan.DownPaymentSessionId, new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await context.Engine.TryCompleteAsync(result.Plan.DownPaymentSessionId, TestContext.Current.CancellationToken);
        await service.ActivateAsync(result.Plan.ItemId, TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPlanStatus.Active, result.Plan.Status);

        return result.Plan;
    }

    private static async Task<Transaction> SeedSeasonFeeAsync(TestContextBuilder context)
    {
        var transaction = new Transaction
        {
            ItemId = "tx-season-fee",
            Title = "Season fee",
            Source = "club-fees",
            OwnerId = CustomerId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            ReferenceType = "Registration",
            ReferenceId = "reg-1",
            Currency = "USD",
            Amount = 300m,
            TotalAmount = 300m,
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now,
            UpdatedUtc = _now,
            Properties = new Dictionary<string, object>
            {
                ["season"] = "2027",
            },
        };

        await context.Transactions.CreateAsync(transaction, TestContext.Current.CancellationToken);

        return transaction;
    }

    // The checkout a customer opens to pay the balance, as the settlement handler and the installment step build it.
    private static CheckoutSession SeedSettlementCheckout(TestContextBuilder context, Transaction transaction, string sessionId = "session-customer")
    {
        var session = new CheckoutSession
        {
            SessionId = sessionId,
            ReferenceType = TransactionsConstants.ReferenceTypes.Transaction,
            ReferenceId = transaction.ItemId,
            Status = CheckoutSessionStatus.Pending,
            Currency = transaction.Currency,
            OwnerId = CustomerId,
            SettlesExistingBalance = true,
            CreatedUtc = _now,
            ModifiedUtc = _now,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = TransactionsConstants.SettlementStepKey,
            Order = 0,
            BillingItems =
            [
                new BillingItem
                {
                    ItemId = transaction.ItemId,
                    Description = transaction.Title,
                    Amount = transaction.TotalAmount,
                    ExcludeFromTax = true,
                },
            ],
        });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = SubscriptionConstants.InstallmentPlans.CheckoutStepKey,
            Order = 10,
            CollectData = true,
        });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
        });

        context.Engine.Sessions.Seed(session);

        return session;
    }

    private static CreateInstallmentPlanFromTransactionRequest ConvertRequest(Transaction transaction, CheckoutSession checkout)
        => new()
        {
            TransactionId = transaction.ItemId,
            CheckoutSessionId = checkout.SessionId,
            Option = new InstallmentOption
            {
                Id = "3-Monthly",
                InstallmentCount = 3,
                Frequency = InstallmentFrequency.Monthly,
            },
        };

    private static CreateInstallmentPlanRequest CreateRequest()
        => new()
        {
            Title = "Kitchen remodel",
            CustomerUserId = CustomerId,
            Currency = "USD",
            TotalAmount = 1200m,
            DownPaymentAmount = 300m,
            InstallmentCount = 6,
            Frequency = InstallmentFrequency.Monthly,
            FirstDueUtc = new DateTime(2027, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            CollectionMethod = InstallmentCollectionMethod.AutoCharge,
        };

    private static async Task<InstallmentPlan> CreateAndPayDownPaymentAsync(TestContextBuilder context, DefaultInstallmentPlanService service, InstallmentCollectionMethod method = InstallmentCollectionMethod.AutoCharge)
    {
        var request = CreateRequest();
        request.CollectionMethod = method;

        var plan = (await service.CreateAsync(request, TestContext.Current.CancellationToken)).Plan;

        await context.Engine.BeginPaymentAsync(plan.DownPaymentSessionId, new BeginPaymentOptions { ProviderKey = ProviderKey }, TestContext.Current.CancellationToken);
        await context.Engine.TryCompleteAsync(plan.DownPaymentSessionId, TestContext.Current.CancellationToken);

        return plan;
    }

    private static async Task<InstallmentPlan> CreateActivePlanAsync(TestContextBuilder context, DefaultInstallmentPlanService service, InstallmentCollectionMethod method = InstallmentCollectionMethod.AutoCharge)
    {
        var plan = await CreateAndPayDownPaymentAsync(context, service, method);

        await service.ActivateAsync(plan.ItemId, TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);

        return plan;
    }

    private sealed class TestContextBuilder
    {
        public FakeTransactionStore Transactions { get; } = new();

        public RecordingTransactionEventHandler TransactionEvents { get; } = new();

        public InMemoryPaymentAttemptStore Attempts { get; } = new();

        public InMemoryInstallmentPlanStore Plans { get; } = new();

        public TestClock Clock { get; } = new(_now);

        public FakePlanCheckoutEngine Engine { get; private set; }

        public Mock<UserManager<IUser>> UserManager { get; } = new(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        public bool HasCardProvider { get; set; } = true;

        public SavedPaymentMethod KeptCard { get; set; } = new()
        {
            ProviderKey = ProviderKey,
            CustomerReference = "cus_saved",
            PaymentMethodReference = "pm_saved",
            Brand = "visa",
            Last4 = "4242",
        };

        public InstallmentPlanSettings Settings { get; set; } = new();

        public Mock<INotificationService> Notifications { get; } = new();

        public FakeChargeService Charges { get; private set; }

        public DefaultInstallmentPlanService Service { get; private set; }

        // As the saved card charge service does when the card declines: one more declined charge, and the next one on
        // the charge's own retry days, or the payment handed over when none is left. Each raises its event.
        public async Task<SavedPaymentMethodChargeResult> DeclineAsync(Transaction transaction, string reason)
        {
            var handler = new InstallmentPlanAutoChargeHandler(Service);
            var charge = transaction.AutoCharge ?? new TransactionAutoCharge { RetryDays = [] };

            charge.Attempts++;
            charge.LastFailure = reason;

            if (charge.GetRetryDelay(charge.Attempts) is { } delay)
            {
                charge.NextAttemptUtc = Clock.UtcNow.Add(delay);
                transaction.AutoCharge = charge;

                await handler.AutoChargeDeclinedAsync(transaction, reason, charge.NextAttemptUtc, TestContext.Current.CancellationToken);

                return new SavedPaymentMethodChargeResult { Transaction = transaction, ChargeStatus = SavedPaymentMethodChargeStatus.Declined, NextAttemptUtc = charge.NextAttemptUtc };
            }

            transaction.AutoCharge = null;
            transaction.AutoCollection = null;

            await handler.AutoChargeAbandonedAsync(transaction, reason, TestContext.Current.CancellationToken);

            return new SavedPaymentMethodChargeResult { Transaction = transaction, ChargeStatus = SavedPaymentMethodChargeStatus.Declined, HandedOver = true };
        }

        // As the saved card charge service does when the charge goes through: the ledger records the payment and its
        // payment handlers run.
        public async Task<SavedPaymentMethodChargeResult> PayAsync(Transaction transaction)
        {
            var amount = transaction.OutstandingAmount;

            transaction.AmountPaid = transaction.TotalAmount;
            transaction.Status = TransactionStatus.Paid;
            transaction.SettledUtc = Clock.UtcNow;
            transaction.AutoCharge = null;

            await new InstallmentPlanTransactionPaymentHandler(Service).PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, new TransactionEvent { Amount = amount }), TestContext.Current.CancellationToken);

            return new SavedPaymentMethodChargeResult { Transaction = transaction, ChargeStatus = SavedPaymentMethodChargeStatus.Succeeded };
        }

        public DefaultInstallmentPlanService Build()
        {
            Charges = new FakeChargeService(this);

            Engine = new FakePlanCheckoutEngine(Transactions, Attempts, new InMemoryCheckoutSessionStore());

            var provider = new Mock<ICheckoutPaymentProvider>();
            provider.SetupGet(candidate => candidate.Key).Returns(ProviderKey);
            provider.SetupGet(candidate => candidate.Capabilities).Returns(new PaymentProviderCapabilities
            {
                SupportsOneTimePayments = true,
                SupportsEmbeddedElements = true,
                SupportsSavedPaymentMethods = HasCardProvider,
            });

            var providerResolver = new Mock<ICheckoutPaymentProviderResolver>();
            providerResolver.Setup(resolver => resolver.GetProviders()).Returns([provider.Object]);

            var savedProvider = new Mock<ICheckoutSavedPaymentMethodProvider>();
            savedProvider.SetupGet(candidate => candidate.Key).Returns(ProviderKey);
            savedProvider
                .Setup(candidate => candidate.GetSavedPaymentMethodAsync(It.IsAny<PaymentAttempt>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => KeptCard);

            var customer = new User { UserId = CustomerId, UserName = "customer", Email = "customer@example.com" };

            var userService = new Mock<IUserService>();
            userService.Setup(service => service.GetUserByUniqueIdAsync(CustomerId)).ReturnsAsync(customer);

            UserManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((User)user).UserId ?? "new-user");
            UserManager.Setup(manager => manager.CreateAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);

            var transactionManager = TransactionManagerFactory.Create(Transactions, TransactionEvents);

            Service = new DefaultInstallmentPlanService(
                Plans,
                transactionManager,
                Engine,
                Engine.Sessions,
                providerResolver.Object,
                [savedProvider.Object],
                Attempts,
                new TransactionSettlementService(
                    transactionManager,
                    new ServiceCollection().BuildServiceProvider(),
                    Clock,
                    NullLogger<TransactionSettlementService>.Instance,
                    new PassThroughStringLocalizer<TransactionSettlementService>()),
                new CheckoutInvoiceBuilder(
                    SiteServiceFactory.Create(Settings),
                    new DefaultCheckoutDiscountService([], NullLogger<DefaultCheckoutDiscountService>.Instance),
                    new NullCheckoutTaxService()),
                UserManager.Object,
                userService.Object,
                new LocalLock(NullLogger<LocalLock>.Instance),
                SiteServiceFactory.Create(Settings),
                new HttpContextAccessor(),
                TestCustomerOwnerNotifier.Create(Notifications.Object, userService.Object),
                new Mock<ISession>().Object,
                Clock,
                NullLogger<DefaultInstallmentPlanService>.Instance,
                new PassThroughStringLocalizer<DefaultInstallmentPlanService>(),
                chargeService: Charges);

            return Service;
        }
    }

    /// <summary>
    /// The saved card charge service as the plan sees it: schedules replace the transaction's charge, and a charge runs
    /// <see cref="OnCharge"/> (by default it goes through).
    /// </summary>
    private sealed class FakeChargeService : ISavedPaymentMethodChargeService
    {
        private readonly TestContextBuilder _context;

        public FakeChargeService(TestContextBuilder context)
        {
            _context = context;
        }

        public List<SavedPaymentMethodChargeRequest> Scheduled { get; } = [];

        public List<string> Charged { get; } = [];

        public Func<Transaction, Task<SavedPaymentMethodChargeResult>> OnCharge { get; set; }

        public Task<SavedPaymentMethodChargeResult> ScheduleAsync(string transactionId, string savedPaymentMethodId, DateTime chargeOnUtc, CancellationToken cancellationToken = default)
            => ScheduleAsync(new SavedPaymentMethodChargeRequest { TransactionId = transactionId, SavedPaymentMethodId = savedPaymentMethodId, ChargeOnUtc = chargeOnUtc }, cancellationToken);

        public async Task<SavedPaymentMethodChargeResult> ScheduleAsync(SavedPaymentMethodChargeRequest request, CancellationToken cancellationToken = default)
        {
            Scheduled.Add(request);

            var transaction = await _context.Transactions.FindByIdAsync(request.TransactionId, cancellationToken);

            transaction.AutoCharge = new TransactionAutoCharge
            {
                SavedPaymentMethodId = request.SavedPaymentMethodId,
                SourceSessionId = request.SourceSessionId,
                RetryDays = request.RetryDays,
                NextAttemptUtc = request.ChargeOnUtc,
            };

            return new SavedPaymentMethodChargeResult { Transaction = transaction };
        }

        public async Task<SavedPaymentMethodChargeResult> ChargeAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            Charged.Add(transactionId);

            var transaction = await _context.Transactions.FindByIdAsync(transactionId, cancellationToken);

            return await (OnCharge ?? _context.PayAsync)(transaction);
        }

        public Task<int> ChargeDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<SavedPaymentMethodChargeResult> CancelAsync(string transactionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new SavedPaymentMethodChargeResult());
    }
}
