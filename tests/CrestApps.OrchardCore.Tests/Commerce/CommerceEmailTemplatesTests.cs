using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Classifieds;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Inventory.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Every kind of commerce email renders as built until the store writes its own template of it, and then the store's
/// template wins, for that kind only.
/// </summary>
public sealed class CommerceEmailTemplatesTests
{
    public static TheoryData<string> Kinds =>
    [
        OrderEmailKinds.Confirmation,
        OrderEmailKinds.Invoice,
        OrderEmailKinds.Quote,
        OrderEmailKinds.Shipped,
        OrderEmailKinds.Return,
        OrderEmailKinds.GiftCard,
        OrderEmailKinds.Recovery,
        BookingEmailSender.EmailKind,
        B2BApprovalService.ApprovalRequestedEmailKind,
        B2BApprovalService.ApprovalDecidedEmailKind,
        StockAlertService.EmailKind,
        ReviewRequestService.EmailKind,
        TransactionEmailKinds.PaymentReminder,
        TransactionEmailKinds.PaymentReceipt,
        TransactionEmailKinds.Refund,
        InstallmentPlanEmailKinds.Notice,
        DonationService.ReceiptEmailKind,
        ClassifiedsConstants.ListingEmailKind,
        PosReceipts.EmailKind,
    ];

    [Fact]
    public void EveryKind_HasItsOwnName()
        => Assert.Equal(Kinds.Count, Kinds.Select(row => row.Data).Distinct(StringComparer.Ordinal).Count());

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task AKind_RendersAsBuilt_UntilTheStoreWritesItsTemplate(string kind)
    {
        var templates = CommerceEmailSenders.Templates(new OrderEmailTemplateSettings());

        var message = await templates.ApplyAsync(Context(kind), TestContext.Current.CancellationToken);

        Assert.Equal("Built subject", message.Subject);
        Assert.Equal("Built text", message.Text);
        Assert.Equal("<p>Built</p>", message.Html);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task TheStoresTemplate_Wins_ForItsKindOnly(string kind)
    {
        var settings = new OrderEmailTemplateSettings();
        settings.Templates[kind] = new CommerceEmailTemplate
        {
            IsEnabled = true,
            Subject = "{{ StoreName }}: {{ Subject }}",
            Body = "<h1>Ours</h1>{{ Content }}<p>{{ Link }}</p>",
        };

        var templates = CommerceEmailSenders.Templates(settings, "Contoso");

        var message = await templates.ApplyAsync(Context(kind), TestContext.Current.CancellationToken);
        var other = await templates.ApplyAsync(Context(kind == OrderEmailKinds.Quote ? OrderEmailKinds.Invoice : OrderEmailKinds.Quote), TestContext.Current.CancellationToken);

        Assert.Equal("Contoso: Built subject", message.Subject);
        Assert.Equal("<h1>Ours</h1><p>Built</p><p>https://store.test/link</p>", message.Html);
        Assert.Equal("Ours\nBuilt\nhttps://store.test/link", message.Text);
        Assert.Equal("<p>Built</p>", other.Html);
    }

    [Fact]
    public async Task ATemplate_ReadsTheEmailsRecords_ByName()
    {
        var settings = new OrderEmailTemplateSettings();
        settings.Templates[OrderEmailKinds.Confirmation] = new CommerceEmailTemplate
        {
            IsEnabled = true,
            Subject = "Order {{ Order.OrderNumber }} ({{ Order.Status }})",
        };

        var templates = CommerceEmailSenders.Templates(settings);
        var context = new CommerceEmailContext
        {
            Kind = OrderEmailKinds.Confirmation,
            Models = OrderEmailModels.For(new Order { OrderNumber = "261104-MAIL", Status = OrderStatus.Paid }),
            Subject = "Built subject",
            Html = "<p>Built</p>",
        };

        var message = await templates.ApplyAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("Order 261104-MAIL (Paid)", message.Subject);
        Assert.Equal("<p>Built</p>", message.Html);
    }

    [Fact]
    public async Task TheLayout_GoesAroundEveryKind_ButATextOnlyEmailStaysText()
    {
        var templates = CommerceEmailSenders.Templates(new OrderEmailTemplateSettings { Layout = "<div class=\"store\">{{ Content }}</div>" });

        var html = await templates.ApplyAsync(Context(StockAlertService.EmailKind), TestContext.Current.CancellationToken);
        var text = await templates.ApplyAsync(new CommerceEmailContext { Kind = InstallmentPlanEmailKinds.Notice, Subject = "Plan", Text = "Your plan changed." }, TestContext.Current.CancellationToken);

        Assert.Equal("<div class=\"store\"><p>Built</p></div>", html.Html);
        Assert.Equal("Built text", html.Text);
        Assert.Null(text.Html);
        Assert.Equal("Your plan changed.", text.Text);
    }

    [Fact]
    public async Task ATemplateThatDoesntRender_SendsTheBuiltEmail()
    {
        var settings = new OrderEmailTemplateSettings();
        settings.Templates[ReviewRequestService.EmailKind] = new CommerceEmailTemplate { IsEnabled = true, Body = "{% if %}" };

        var message = await CommerceEmailSenders.Templates(settings).ApplyAsync(Context(ReviewRequestService.EmailKind), TestContext.Current.CancellationToken);

        Assert.Equal("<p>Built</p>", message.Html);
    }

    [Fact]
    public async Task ABuyersMessage_WithAKind_GoesThroughTheStoresTemplate()
    {
        var settings = new OrderEmailTemplateSettings();
        settings.Templates[TransactionEmailKinds.PaymentReceipt] = new CommerceEmailTemplate
        {
            IsEnabled = true,
            Subject = "Thanks from {{ StoreName }}",
            Body = "<p>Paid: {{ Transaction.Title }}</p>",
        };

        NotificationMessage sent = null;
        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(value => value.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<object, INotificationMessage, CancellationToken>((_, message, _) => sent = (NotificationMessage)message)
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        var users = new Mock<IUserService>();
        users.Setup(value => value.GetUserByUniqueIdAsync("user-1")).ReturnsAsync(Mock.Of<IUser>());

        var services = new ServiceCollection()
            .AddSingleton<ICommerceEmailTemplates>(CommerceEmailSenders.Templates(settings, "Contoso"))
            .BuildServiceProvider();

        var notifier = TestCustomerOwnerNotifier.Create(notifications.Object, users.Object, services: services);

        var delivered = await notifier.NotifyAsync(CustomerOwner.ForUser("user-1"), null, new CustomerOwnerMessage
        {
            Kind = TransactionEmailKinds.PaymentReceipt,
            Models = new Dictionary<string, object> { [TransactionEmailKinds.TransactionModelName] = new { Title = "Invoice 12" } },
            Subject = "Receipt",
            TextBody = "You paid.",
            HtmlBody = "<p>You paid.</p>",
        }, TestContext.Current.CancellationToken);

        Assert.True(delivered);
        Assert.Equal("Thanks from Contoso", sent.Subject);
        Assert.Equal("<p>Paid: Invoice 12</p>", sent.HtmlBody);
        Assert.Equal("Paid: Invoice 12", sent.TextBody);
    }

    private static CommerceEmailContext Context(string kind)
        => new()
        {
            Kind = kind,
            Subject = "Built subject",
            Text = "Built text",
            Html = "<p>Built</p>",
            Values = new Dictionary<string, string> { ["Link"] = "https://store.test/link" },
        };
}
