using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
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
    [InlineData(1200, 0, 6, nameof(CreateInstallmentPlanRequest.DownPaymentAmount))]
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
    public async Task ProcessAsync_WhenAPaymentFallsDue_ChargesTheSavedCardOnceAndRecordsIt()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var charge = Assert.Single(context.Engine.Begun.Skip(1));

        Assert.Equal(ProviderKey, charge.Options.ProviderKey);
        Assert.True(CheckoutPaymentDataKeys.IsSet(charge.Options.ProviderData, CheckoutPaymentDataKeys.OffSession));
        Assert.Equal("pm_saved", charge.Options.ProviderData[CheckoutPaymentDataKeys.SavedPaymentMethodReference]);
        Assert.Equal(CustomerId, context.Engine.Started.Last().OwnerId);

        Assert.Equal(InstallmentPaymentStatus.Paid, plan.Payments[1].Status);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, plan.Payments[2].Status);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);

        // A second sweep the same day charges nothing more.
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, context.Engine.Begun.Count);
    }

    [Fact]
    public async Task ProcessAsync_WhenTheCardIsDeclined_SchedulesARetryAndMarksThePlanPastDue()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = false, Declined = true, ProviderErrorMessage = "Your card has insufficient funds." };
        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var payment = plan.Payments[1];

        Assert.Equal(InstallmentPaymentStatus.Failed, payment.Status);
        Assert.Equal(1, payment.ChargeAttempts);
        Assert.Equal("Your card has insufficient funds.", payment.LastFailureMessage);
        Assert.Equal(context.Clock.UtcNow.AddDays(1), payment.NextChargeAttemptUtc);
        Assert.Equal(InstallmentPlanStatus.PastDue, plan.Status);
        Assert.Single(context.Engine.Canceled);

        // Before the retry is due nothing is charged.
        context.Clock.UtcNow = context.Clock.UtcNow.AddHours(12);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, context.Engine.Begun.Count);
    }

    [Fact]
    public async Task ProcessAsync_AfterTheLastRetryFails_HandsThePaymentToTheCustomer()
    {
        // Arrange
        var context = new TestContextBuilder { Settings = new InstallmentPlanSettings { RetryDays = [1] } };
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = false, Declined = true, ProviderErrorMessage = "Declined." };
        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        context.Clock.UtcNow = context.Clock.UtcNow.AddDays(1).AddMinutes(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);

        Assert.Equal(2, payment.ChargeAttempts);
        Assert.Null(payment.NextChargeAttemptUtc);
        Assert.Equal(TransactionStatus.Outstanding, transaction.Status);
        Assert.Null(transaction.AutoCollection);
        Assert.Equal(InstallmentPlanStatus.PastDue, plan.Status);

        // A payment handed to the customer is not charged again by the schedule.
        context.Clock.UtcNow = context.Clock.UtcNow.AddDays(10);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, context.Engine.Begun.Count);
    }

    [Fact]
    public async Task ProcessAsync_WhenTheGatewayCannotBeReached_RetriesSoonWithoutUsingARetry()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = false, Declined = false, ErrorMessage = "The payment could not be started." };
        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var payment = plan.Payments[1];

        Assert.NotEqual(InstallmentPaymentStatus.Failed, payment.Status);
        Assert.Equal(0, payment.ChargeAttempts);
        Assert.Equal(context.Clock.UtcNow.AddHours(1), payment.NextChargeAttemptUtc);
    }

    [Fact]
    public async Task ProcessAsync_WhileAChargeIsStillSettling_FinishesItInsteadOfChargingAgain()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Engine.CompletionStatus = CheckoutCompletionStatus.Pending;
        context.Clock.UtcNow = plan.Payments[1].DueUtc.AddHours(1);

        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPaymentStatus.Due, plan.Payments[1].Status);

        context.Engine.CompletionStatus = CheckoutCompletionStatus.Completed;

        // Act
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, context.Engine.Begun.Count);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.Payments[1].Status);
    }

    [Fact]
    public async Task ProcessAsync_WhenAChargeWasTakenButNeverRecorded_AppliesItInsteadOfChargingAgain()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];
        var transaction = await context.Transactions.FindByIdAsync(payment.TransactionId, TestContext.Current.CancellationToken);
        var unpaidStatus = transaction.Status;
        var eventCount = transaction.Events.Count;

        context.Clock.UtcNow = payment.DueUtc.AddHours(1);

        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // The request that recorded the charge failed after the gateway took the money, so everything it wrote was
        // rolled back: the plan forgot it charged the card and the transaction never saw the payment. Only the
        // payment attempt, committed before the charge, survives.
        Assert.Single(await context.Attempts.GetByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, TestContext.Current.CancellationToken));

        payment.CheckoutSessionIds.Clear();
        payment.ChargeAttempts = 0;
        payment.Status = InstallmentPaymentStatus.Due;
        payment.PaidUtc = null;
        transaction.Status = unpaidStatus;
        transaction.AmountPaid = 0m;
        transaction.SettledUtc = null;

        while (transaction.Events.Count > eventCount)
        {
            transaction.Events.RemoveAt(transaction.Events.Count - 1);
        }

        // Act
        await service.ChargeAsync(plan.ItemId, payment.Number, TestContext.Current.CancellationToken);
        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, context.Engine.Begun.Count);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(payment.Amount, transaction.AmountPaid);
        Assert.Equal(InstallmentPaymentStatus.Paid, payment.Status);
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
    public async Task ChargeAsync_WhenDeclined_ReportsWhyAndSchedulesNoAutomaticRetry()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);

        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = false, Declined = true, ProviderErrorMessage = "Your card was declined." };
        context.Clock.UtcNow = plan.Payments[3].DueUtc.AddHours(1);

        // Act
        var result = await service.ChargeAsync(plan.ItemId, 3, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Value == "Your card was declined.");
        Assert.Equal(InstallmentPaymentStatus.Failed, plan.Payments[3].Status);
        Assert.Null(plan.Payments[3].NextChargeAttemptUtc);
    }

    [Fact]
    public async Task ChargeAsync_WhenAnEarlyChargeIsDeclined_KeepsThePaymentOnScheduleAndChargesItWhenDue()
    {
        // Arrange
        var context = new TestContextBuilder();
        var service = context.Build();
        var plan = await CreateActivePlanAsync(context, service);
        var payment = plan.Payments[1];

        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = false, Declined = true, ProviderErrorMessage = "Your card was declined." };

        // Act
        var result = await service.ChargeAsync(plan.ItemId, payment.Number, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, payment.Status);
        Assert.Equal("Your card was declined.", payment.LastFailureMessage);
        Assert.Equal(0, payment.ChargeAttempts);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);

        // The card works again by the due date, and the payment is charged then as planned.
        context.Engine.OnBegin = _ => new PaymentBeginOutcome { Succeeded = true };
        context.Clock.UtcNow = payment.DueUtc.AddHours(1);

        await service.ProcessAsync(plan.ItemId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(InstallmentPaymentStatus.Paid, payment.Status);
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
        Assert.True(result.Succeeded);
        Assert.Equal(InstallmentPaymentStatus.Paid, plan.Payments[2].Status);
        Assert.Equal(InstallmentPaymentStatus.Scheduled, plan.Payments[1].Status);
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

        public DefaultInstallmentPlanService Build()
        {
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

            var transactionManager = TransactionManagerFactory.Create(Transactions);

            return new DefaultInstallmentPlanService(
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
                UserManager.Object,
                userService.Object,
                new LocalLock(NullLogger<LocalLock>.Instance),
                SiteServiceFactory.Create(Settings),
                new HttpContextAccessor(),
                new ServiceCollection().AddSingleton(Notifications.Object).BuildServiceProvider(),
                new Mock<ISession>().Object,
                Clock,
                NullLogger<DefaultInstallmentPlanService>.Instance,
                new PassThroughStringLocalizer<DefaultInstallmentPlanService>());
        }
    }
}
