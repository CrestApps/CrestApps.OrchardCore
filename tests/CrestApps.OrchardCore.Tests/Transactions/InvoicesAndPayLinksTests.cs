using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A customer who pays an invoice themselves is told what they owe with a numbered invoice and a link that pays it
/// without signing in, which a customer an administrator created (with no password) or a guest needs.
/// </summary>
public sealed class InvoicesAndPayLinksTests
{
    private static readonly DateTime _now = new(2027, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PayLink_UsesTheSiteAddressAndReadsBackTheTransaction()
    {
        // Arrange
        var payLinks = CreatePayLinkService();

        // Act
        var url = await payLinks.CreatePayUrlAsync(new Transaction { ItemId = "tx-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("https://shop.example.com/transactions/pay/", url, StringComparison.Ordinal);
        Assert.Equal("tx-1", payLinks.GetTransactionId(Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..])));
    }

    [Fact]
    public async Task PayLink_ThatWasAltered_OrMadeElsewhere_NamesNoTransaction()
    {
        // Arrange
        var payLinks = CreatePayLinkService();
        var url = await payLinks.CreatePayUrlAsync(new Transaction { ItemId = "tx-1" }, TestContext.Current.CancellationToken);
        var token = Uri.UnescapeDataString(url[(url.LastIndexOf('/') + 1)..]);
        var altered = token[..^2] + (token[^2] == 'A' ? "BB" : "AA");
        var fromAnotherSite = await CreatePayLinkService().CreatePayUrlAsync(new Transaction { ItemId = "tx-1" }, TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Null(payLinks.GetTransactionId(altered));
        Assert.Null(payLinks.GetTransactionId(Uri.UnescapeDataString(fromAnotherSite[(fromAnotherSite.LastIndexOf('/') + 1)..])));
        Assert.Null(payLinks.GetTransactionId("not-a-token"));
    }

    [Fact]
    public async Task InvoiceNumber_IsIssuedOnceAndNeverForAnAutomaticCharge()
    {
        // Arrange
        var generator = CreateNumberGenerator();
        var invoices = new TransactionInvoiceService(generator.Object);
        var invoiced = new Transaction { ItemId = "tx-1" };
        var charged = new Transaction { ItemId = "tx-2", AutoCollection = new TransactionAutoCollection() };

        // Act
        var first = await invoices.EnsureInvoiceNumberAsync(invoiced, TestContext.Current.CancellationToken);
        var second = await invoices.EnsureInvoiceNumberAsync(invoiced, TestContext.Current.CancellationToken);
        var automatic = await invoices.EnsureInvoiceNumberAsync(charged, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.False(automatic);
        Assert.Equal("INV-1001", invoiced.InvoiceNumber);
        Assert.Null(charged.InvoiceNumber);
        generator.Verify(candidate => candidate.GenerateAsync(It.IsAny<FinancialDocumentNumberRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpcomingReminder_ForAnInvoice_CarriesTheNumberAndALinkThatPaysIt()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateReminderService(message => sent = message, delivered: true);
        var transaction = CreateInvoicedTransaction(dueUtc: _now.AddDays(3));

        // Act
        var result = await service.SendUpcomingReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Equal("INV-1001", transaction.InvoiceNumber);
        Assert.Contains("INV-1001", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://shop.example.com/transactions/pay/token", sent.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("sign in", sent.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.True(sent.IsHtmlPreferred);
        Assert.Contains("href=\"https://shop.example.com/transactions/pay/token\"", sent.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstOverdueReminder_OnTheDueDate_SaysThePaymentIsDueToday()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateReminderService(message => sent = message, delivered: true);
        var transaction = CreateInvoicedTransaction(dueUtc: _now.Date);
        transaction.Status = TransactionStatus.Outstanding;

        // Act
        await service.SendReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("due today", sent.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INV-1001", sent.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reminder_ThatCouldNotBeDelivered_KeepsTheInvoiceNumberItWasGiven()
    {
        // Arrange
        var manager = new Mock<ITransactionManager>();
        var service = CreateReminderService(_ => { }, delivered: false, manager: manager.Object);
        var transaction = CreateInvoicedTransaction(dueUtc: _now.AddDays(3));

        // Act
        var result = await service.SendUpcomingReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
        Assert.Equal("INV-1001", transaction.InvoiceNumber);
        manager.Verify(candidate => candidate.UpdateAsync(transaction, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Transaction CreateInvoicedTransaction(DateTime dueUtc)
        => new()
        {
            ItemId = "tx-1",
            Title = "Kitchen remodel — payment 2 of 6",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            TotalAmount = 150m,
            Status = TransactionStatus.Pending,
            DueUtc = dueUtc,
        };

    private static TransactionPayLinkService CreatePayLinkService()
    {
        var site = Mock.Of<ISite>(candidate => candidate.BaseUrl == "https://shop.example.com/");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(candidate => candidate.GetSiteSettingsAsync()).ReturnsAsync(site);

        return new TransactionPayLinkService(new EphemeralDataProtectionProvider(), siteService.Object, new HttpContextAccessor(), Mock.Of<LinkGenerator>());
    }

    private static Mock<IFinancialDocumentNumberGenerator> CreateNumberGenerator()
    {
        var generator = new Mock<IFinancialDocumentNumberGenerator>();
        generator
            .Setup(candidate => candidate.GenerateAsync(It.Is<FinancialDocumentNumberRequest>(request => request.Kind == FinancialDocumentKind.Invoice), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialDocumentNumber(1001, "INV-1001"));

        return generator;
    }

    private static DefaultTransactionReminderService CreateReminderService(Action<INotificationMessage> onSend, bool delivered, ITransactionManager manager = null)
    {
        var userService = new Mock<IUserService>();
        userService.Setup(candidate => candidate.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(Mock.Of<IUser>());

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(candidate => candidate.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => onSend(message))
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = delivered ? 1 : 0 });

        var payLinks = new Mock<ITransactionPayLinkService>();
        payLinks
            .Setup(candidate => candidate.CreatePayUrlAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://shop.example.com/transactions/pay/token");

        var services = new ServiceCollection()
            .AddSingleton<ITransactionInvoiceService>(new TransactionInvoiceService(CreateNumberGenerator().Object))
            .AddSingleton(payLinks.Object);

        if (manager is not null)
        {
            services.AddSingleton(manager);
        }

        return new DefaultTransactionReminderService(
            notifications.Object,
            userService.Object,
            Mock.Of<ICustomerContactResolver>(),
            services.BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }
}
