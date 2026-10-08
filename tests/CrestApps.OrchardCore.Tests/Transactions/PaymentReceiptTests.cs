using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The customer gets proof of every payment, however it was made: one receipt per payment, for the money in that
/// payment, with its share of the tax.
/// </summary>
public sealed class PaymentReceiptTests
{
    [Fact]
    public async Task BuildAsync_ForAPartPayment_ShowsThatPaymentAndItsShareOfTheTax()
    {
        // Arrange
        var builder = CreateBuilder();
        var transaction = CreateTransaction();
        var payment = TransactionPaymentHandlerExtensions.CreatePaymentEvent(DateTime.UtcNow, 54m, TransactionsConstants.SettlementMethods.Offline, "Paid by check.");

        // Act
        var receipt = await builder.BuildAsync(transaction, payment, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(54m, receipt.Total);
        Assert.Equal(4m, receipt.TaxAmount);
        Assert.Equal(50m, Assert.Single(receipt.LineItems).Amount);
        Assert.Equal(ReceiptStatus.Paid, receipt.Status);
        Assert.Equal("Paid offline.", receipt.Notes);
        Assert.Equal("Jane", receipt.BilledToName);
        Assert.EndsWith(payment.Id, receipt.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildAsync_ForAnEventThatMovedNoMoney_ReturnsNothing()
    {
        // Arrange
        var builder = CreateBuilder();

        // Act
        var receipt = await builder.BuildAsync(CreateTransaction(), new TransactionEvent { Type = TransactionEventType.Note }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(receipt);
    }

    [Fact]
    public void Render_EncodesEverythingTakenFromTheReceipt()
    {
        // Arrange
        var renderer = new DefaultReceiptHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<DefaultReceiptHtmlRenderer>());

        // Act
        var html = renderer.Render(new ReceiptDocument
        {
            BusinessName = "<script>alert(1)</script>",
            Currency = "USD",
            Total = 10m,
            LineItems = [new ReceiptLineItem { Description = "<b>Plan</b>", Quantity = 1, Amount = 10m }],
        });

        // Assert
        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Plan</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_SendsTheReceiptToASignedUpCustomerAndRecordsIt()
    {
        // Arrange
        INotificationMessage sent = null;

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => sent = message)
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(Mock.Of<IUser>());

        var transaction = CreateTransaction();
        var store = new FakeTransactionStore(transaction);
        var handler = CreateHandler(store, notifications.Object, userService.Object);
        var payment = TransactionPaymentHandlerExtensions.CreatePaymentEvent(DateTime.UtcNow, 108m, TransactionsConstants.SettlementMethods.Online, "Paid.");

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, payment), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sent);
        Assert.True(sent.IsHtmlPreferred);
        Assert.Contains("USD 108.00", sent.Subject, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(sent.HtmlBody));
        Assert.Contains(transaction.Events, evt => evt.Type == TransactionEventType.Note && evt.Message.Contains("receipt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handler_EmailsAGuestTheReceipt()
    {
        // Arrange
        MailMessage sent = null;

        var email = new Mock<IEmailService>();
        email
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((MailMessage message, string _, CancellationToken _) => sent = message)
            .ReturnsAsync(Result.Success());

        var transaction = CreateTransaction();
        transaction.OwnerKind = CustomerOwnerKind.Guest;
        transaction.GuestContactEmail = "guest@example.com";

        var handler = CreateHandler(new FakeTransactionStore(transaction), Mock.Of<INotificationService>(), Mock.Of<IUserService>(), email.Object);

        // Act
        await handler.PaymentRecordedAsync(
            new TransactionPaymentRecordedContext(transaction, TransactionPaymentHandlerExtensions.CreatePaymentEvent(DateTime.UtcNow, 108m, TransactionsConstants.SettlementMethods.Online, "Paid.")),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("guest@example.com", sent?.To);
        Assert.False(string.IsNullOrEmpty(sent.HtmlBody));
    }

    private static Transaction CreateTransaction()
        => new()
        {
            ItemId = "tx-1",
            Title = "Kitchen remodel",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            GuestContactName = "Jane",
            Currency = "USD",
            Amount = 100m,
            TaxAmount = 8m,
            TotalAmount = 108m,
        };

    private static TransactionReceiptBuilder CreateBuilder()
    {
        var contacts = new Mock<ICustomerContactResolver>();
        contacts
            .Setup(resolver => resolver.ResolveAsync(It.IsAny<CustomerOwner>(), It.IsAny<ICustomerContact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerOwner _, ICustomerContact guest, CancellationToken _) => guest);

        var receipts = new Mock<IReceiptService>();
        receipts
            .Setup(service => service.BuildAsync(It.IsAny<ReceiptRequest>()))
            .ReturnsAsync((ReceiptRequest request) => new ReceiptDocument
            {
                BilledToName = request.BilledToName,
                BilledToEmail = request.BilledToEmail,
                Reference = request.Reference,
                Currency = request.Currency,
                LineItems = request.LineItems,
                TaxLines = request.TaxLines,
                TaxAmount = request.TaxAmount,
                Total = request.Total,
                Subtotal = request.Total - request.TaxAmount,
                Status = request.Status,
                Notes = request.Notes,
            });

        return new TransactionReceiptBuilder(
            receipts.Object,
            contacts.Object,
            new ServiceCollection().BuildServiceProvider(),
            new PassThroughStringLocalizer<TransactionReceiptBuilder>());
    }

    private static PaymentReceiptTransactionPaymentHandler CreateHandler(FakeTransactionStore store, INotificationService notifications, IUserService userService, IEmailService email = null)
    {
        var services = new ServiceCollection();

        if (email is not null)
        {
            services.AddSingleton(email);
        }

        return new PaymentReceiptTransactionPaymentHandler(
            CreateBuilder(),
            new DefaultReceiptHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<DefaultReceiptHtmlRenderer>()),
            TransactionManagerFactory.Create(store),
            notifications,
            userService,
            services.BuildServiceProvider(),
            new TestClock(DateTime.UtcNow),
            NullLogger<PaymentReceiptTransactionPaymentHandler>.Instance,
            new PassThroughStringLocalizer<PaymentReceiptTransactionPaymentHandler>());
    }
}
