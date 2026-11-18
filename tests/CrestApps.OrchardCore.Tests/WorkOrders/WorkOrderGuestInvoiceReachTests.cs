using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.WorkOrders;

/// <summary>
/// A work order with no customer user is owned as a guest (the work order id), and nothing but its contact can reach the
/// customer: the reminders and the receipt go to the contact's e-mail. The pay link carries only the transaction id, so it
/// works for a guest owner as for any other.
/// </summary>
public sealed class WorkOrderGuestInvoiceReachTests
{
    [Fact]
    public async Task AReminder_ReachesTheWorkOrdersContact()
    {
        // Arrange
        var transaction = await WorkOrderIntegrationTests.InvoiceWithoutUserAsync();
        var email = CaptureEmail(out var sent);
        var contacts = new Mock<ICustomerContactResolver>();
        contacts
            .Setup(resolver => resolver.ResolveAsync(It.IsAny<CustomerOwner>(), It.IsAny<ICustomerContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerOwner _, ICustomerContact guest, CancellationToken _) => guest);

        var services = new ServiceCollection().AddSingleton(email.Object).BuildServiceProvider();
        var reminders = new DefaultTransactionReminderService(
            TestCustomerOwnerNotifier.Create(new Mock<INotificationService>(MockBehavior.Strict).Object, new Mock<IUserService>(MockBehavior.Strict).Object, contacts.Object, services),
            services,
            new TestClock(new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc)),
            null,
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());

        // Act
        var delivered = await reminders.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(delivered);
        Assert.Equal("lee@contoso.example", Assert.Single(sent).To);
    }

    [Fact]
    public async Task TheReceipt_ReachesTheWorkOrdersContact()
    {
        // Arrange
        var transaction = await WorkOrderIntegrationTests.InvoiceWithoutUserAsync();
        var email = CaptureEmail(out var sent);
        var handler = PaymentReceiptTests.CreateHandler(new FakeTransactionStore(transaction), Mock.Of<INotificationService>(), Mock.Of<IUserService>(), email.Object);

        // Act
        await handler.PaymentRecordedAsync(
            new TransactionPaymentRecordedContext(transaction, TransactionPaymentHandlerExtensions.CreatePaymentEvent(new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc), 900m, TransactionsConstants.SettlementMethods.Online, "Paid.")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("lee@contoso.example", Assert.Single(sent).To);
    }

    private static Mock<IEmailService> CaptureEmail(out List<MailMessage> sent)
    {
        var messages = new List<MailMessage>();
        var email = new Mock<IEmailService>();
        email
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((MailMessage message, string _, CancellationToken _) => messages.Add(message))
            .ReturnsAsync(Result.Success());

        sent = messages;

        return email;
    }
}
