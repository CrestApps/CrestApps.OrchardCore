using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Receipts.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Framework.Mvc;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Controllers;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.FinancialDocuments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using CrestApps.OrchardCore.Transactions.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Notifications;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Services;
using Xunit;
using TransactionsStartup = CrestApps.OrchardCore.Transactions.Startup;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// A customer who never uses the admin can see their statement, invoices and receipts in the site theme, a module can
/// build the same documents from the abstractions, and the links sent in receipts and reminders can open those pages.
/// </summary>
public sealed class TransactionCustomerPagesTests
{
    private static readonly DateTime _now = new(2027, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheBuildersAndTheirViewModels_LiveInTheTransactionsAbstractions()
    {
        // Arrange
        var abstractions = typeof(TransactionsConstants).Assembly;

        // Act & Assert
        Assert.Equal(abstractions, typeof(ITransactionInvoiceBuilder).Assembly);
        Assert.Equal(abstractions, typeof(ITransactionReceiptBuilder).Assembly);
        Assert.Equal(abstractions, typeof(TransactionInvoiceViewModel).Assembly);
        Assert.Equal(abstractions, typeof(TransactionReceiptViewModel).Assembly);
        Assert.Equal(abstractions, typeof(TransactionStatementViewModel).Assembly);
        Assert.Equal(abstractions, typeof(ITransactionCustomerPageService).Assembly);
        Assert.Equal(abstractions, typeof(ITransactionCustomerLinkService).Assembly);
        Assert.Equal("CrestApps.OrchardCore.Transactions.Abstractions", abstractions.GetName().Name);
    }

    [Fact]
    public void TheBuilders_ResolveThroughTheirAbstractions_WhenTheReceiptsModuleIsEnabled()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton(Mock.Of<IReceiptService>());
        services.AddSingleton(Mock.Of<ICustomerContactResolver>());
        services.AddSingleton(Mock.Of<ILocalClock>());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        new ReceiptPagesStartup().ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var invoices = scope.ServiceProvider.GetService<ITransactionInvoiceBuilder>();
        var receipts = scope.ServiceProvider.GetService<ITransactionReceiptBuilder>();

        // Assert
        Assert.IsType<TransactionInvoiceBuilder>(invoices);
        Assert.IsType<TransactionReceiptBuilder>(receipts);
    }

    [Fact]
    public void TheSiteThemePages_AreFrontEndRoutes_WithTheirOwnNames()
    {
        // Arrange
        var controller = typeof(MyTransactionsController);

        // Act
        var index = GetGetRoute(controller, nameof(MyTransactionsController.Index));
        var invoice = GetGetRoute(controller, nameof(MyTransactionsController.Invoice));
        var receipt = GetGetRoute(controller, nameof(MyTransactionsController.Receipt));

        // Assert
        Assert.Null(controller.GetCustomAttribute<AdminAttribute>());
        Assert.Null(controller.GetCustomAttribute<CustomerAdminPageAttribute>());
        Assert.Equal(("my-transactions", TransactionsConstants.RouteNames.Statement), index);
        Assert.Equal(("my-transactions/invoices/{itemId}", TransactionsConstants.RouteNames.Invoice), invoice);
        Assert.Equal(("my-transactions/receipts/{itemId}/{paymentId}", TransactionsConstants.RouteNames.Receipt), receipt);
        Assert.All(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), method => Assert.Null(method.GetCustomAttribute<AdminAttribute>()));
    }

    [Fact]
    public void TheAdminPages_StayCustomerAdminPages()
    {
        // Act & Assert
        Assert.NotNull(typeof(TransactionInvoiceController).GetCustomAttribute<AdminAttribute>());
        Assert.NotNull(typeof(TransactionReceiptController).GetCustomAttribute<AdminAttribute>());
        Assert.NotNull(typeof(TransactionInvoiceController).GetCustomAttribute<CustomerAdminPageAttribute>());
        Assert.NotNull(typeof(TransactionReceiptController).GetCustomAttribute<CustomerAdminPageAttribute>());
    }

    [Fact]
    public async Task SiteInvoicePage_ForAVisitorWhoIsNotSignedIn_AsksThemToSignIn()
    {
        // Arrange
        var pages = new Mock<ITransactionCustomerPageService>(MockBehavior.Strict);
        var controller = CreateSiteController(pages.Object, new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        var invoice = await controller.Invoice("tx-1");
        var receipt = await controller.Receipt("tx-1", "payment-1");

        // Assert
        Assert.IsType<ChallengeResult>(invoice);
        Assert.IsType<ChallengeResult>(receipt);
        pages.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(TransactionCustomerPageStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(TransactionCustomerPageStatus.NotFound, typeof(NotFoundResult))]
    public async Task SitePages_AnswerAsTheOwnershipCheckSays(TransactionCustomerPageStatus status, Type expected)
    {
        // Arrange
        var user = TransactionCustomerPageServiceTests.CreateUser("customer-2");
        var pages = new Mock<ITransactionCustomerPageService>();
        pages
            .Setup(service => service.BuildInvoiceAsync(user, "tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status == TransactionCustomerPageStatus.Forbidden
                ? TransactionCustomerPageResult<TransactionInvoiceViewModel>.Forbidden()
                : TransactionCustomerPageResult<TransactionInvoiceViewModel>.NotFound());
        pages
            .Setup(service => service.BuildReceiptAsync(user, "tx-1", "payment-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status == TransactionCustomerPageStatus.Forbidden
                ? TransactionCustomerPageResult<TransactionReceiptViewModel>.Forbidden()
                : TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound());

        var controller = CreateSiteController(pages.Object, user);

        // Act
        var invoice = await controller.Invoice("tx-1");
        var receipt = await controller.Receipt("tx-1", "payment-1");

        // Assert
        Assert.IsType(expected, invoice);
        Assert.IsType(expected, receipt);
    }

    [Theory]
    [InlineData(TransactionCustomerPageStatus.Forbidden, typeof(ForbidResult))]
    [InlineData(TransactionCustomerPageStatus.NotFound, typeof(NotFoundResult))]
    public async Task AdminPages_AnswerAsTheSameOwnershipCheckSays(TransactionCustomerPageStatus status, Type expected)
    {
        // Arrange
        var user = TransactionCustomerPageServiceTests.CreateUser("customer-2");
        var pages = new Mock<ITransactionCustomerPageService>();
        pages
            .Setup(service => service.BuildInvoiceAsync(user, "tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status == TransactionCustomerPageStatus.Forbidden
                ? TransactionCustomerPageResult<TransactionInvoiceViewModel>.Forbidden()
                : TransactionCustomerPageResult<TransactionInvoiceViewModel>.NotFound());
        pages
            .Setup(service => service.BuildReceiptAsync(user, "tx-1", "payment-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(status == TransactionCustomerPageStatus.Forbidden
                ? TransactionCustomerPageResult<TransactionReceiptViewModel>.Forbidden()
                : TransactionCustomerPageResult<TransactionReceiptViewModel>.NotFound());

        var invoiceController = new TransactionInvoiceController(pages.Object)
        {
            ControllerContext = CreateControllerContext(user),
        };

        var receiptController = new TransactionReceiptController(pages.Object)
        {
            ControllerContext = CreateControllerContext(user),
        };

        // Act
        var invoice = await invoiceController.Index("tx-1");
        var receipt = await receiptController.Index("tx-1", "payment-1");

        // Assert
        Assert.IsType(expected, invoice);
        Assert.IsType(expected, receipt);
    }

    [Fact]
    public async Task AdminInvoicePage_ShowsTheInvoiceTheServiceBuilt()
    {
        // Arrange
        var user = TransactionCustomerPageServiceTests.CreateUser("customer-1");
        var model = new TransactionInvoiceViewModel
        {
            Transaction = TransactionCustomerPageServiceTests.CreateInvoicedTransaction(),
            Invoice = new ReceiptDocument(),
        };

        var pages = new Mock<ITransactionCustomerPageService>();
        pages
            .Setup(service => service.BuildInvoiceAsync(user, "tx-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransactionCustomerPageResult<TransactionInvoiceViewModel>.Found(model));

        var controller = new TransactionInvoiceController(pages.Object)
        {
            ControllerContext = CreateControllerContext(user),
        };

        // Act
        var result = await controller.Index("tx-1");

        // Assert
        Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
    }

    [Theory]
    [InlineData(TransactionCustomerLinkTarget.Site, "https://shop.example.com/my-transactions/invoices/tx-1", "https://shop.example.com/my-transactions/receipts/tx-1/payment-1", "https://shop.example.com/my-transactions")]
    [InlineData(TransactionCustomerLinkTarget.Admin, "https://shop.example.com/Admin/transaction-invoices/tx-1", "https://shop.example.com/Admin/transaction-receipts/tx-1/payment-1", "https://shop.example.com/Admin/my-transactions")]
    public async Task CustomerLinks_OpenThePagesTheOptionNames(TransactionCustomerLinkTarget target, string invoiceUrl, string receiptUrl, string statementUrl)
    {
        // Arrange
        var links = CreateLinkService(target);
        var transaction = TransactionCustomerPageServiceTests.CreateInvoicedTransaction();

        // Act
        var invoice = await links.GetInvoiceUrlAsync(transaction, TestContext.Current.CancellationToken);
        var receipt = await links.GetReceiptUrlAsync(transaction, transaction.Events.Last(), TestContext.Current.CancellationToken);
        var statement = await links.GetStatementUrlAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(invoiceUrl, invoice);
        Assert.Equal(receiptUrl, receipt);
        Assert.Equal(statementUrl, statement);
    }

    [Fact]
    public async Task CustomerLinks_ToTheAdmin_UseTheConfiguredAdminPrefix()
    {
        // Arrange
        var links = CreateLinkService(TransactionCustomerLinkTarget.Admin, adminPrefix: "backoffice");

        // Act
        var invoice = await links.GetInvoiceUrlAsync(TransactionCustomerPageServiceTests.CreateInvoicedTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("https://shop.example.com/backoffice/transaction-invoices/tx-1", invoice);
    }

    [Fact]
    public async Task CustomerLinks_ForAPaymentWithoutAnIdentifier_HaveNoReceipt()
    {
        // Arrange
        var links = CreateLinkService(TransactionCustomerLinkTarget.Site);

        // Act
        var receipt = await links.GetReceiptUrlAsync(
            TransactionCustomerPageServiceTests.CreateInvoicedTransaction(),
            new TransactionEvent { Type = TransactionEventType.PaymentRecorded, Amount = 10m },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(receipt);
    }

    [Theory]
    [InlineData("Site", TransactionCustomerLinkTarget.Site)]
    [InlineData("Admin", TransactionCustomerLinkTarget.Admin)]
    [InlineData(null, TransactionCustomerLinkTarget.Admin)]
    public void TheLinkTarget_IsReadFromTheTenantConfiguration(string configured, TransactionCustomerLinkTarget expected)
    {
        // Arrange
        var values = new Dictionary<string, string>();

        if (configured is not null)
        {
            values["CrestApps:Transactions:CustomerPages:LinkTarget"] = configured;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();

        new TransactionsStartup(new TestShellConfiguration(configuration)).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<TransactionCustomerPagesOptions>>().Value;

        // Assert
        Assert.Equal(expected, options.LinkTarget);
        Assert.Contains(services, service => service.ServiceType == typeof(ITransactionCustomerPageService) && service.ImplementationType == typeof(TransactionCustomerPageService));
        Assert.Contains(services, service => service.ServiceType == typeof(ITransactionCustomerLinkService) && service.ImplementationType == typeof(TransactionCustomerLinkService));
    }

    [Fact]
    public async Task ReceiptMessage_ToASignedUpCustomer_LinksTheReceiptPageTheSiteChose()
    {
        // Arrange
        INotificationMessage sent = null;

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => sent = message)
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        var transaction = TransactionCustomerPageServiceTests.CreateInvoicedTransaction();
        var payment = transaction.Events.Last();
        var handler = CreateReceiptHandler(transaction, notifications.Object, CreateLinkService(TransactionCustomerLinkTarget.Site));

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, payment), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sent);
        Assert.Contains("https://shop.example.com/my-transactions/receipts/tx-1/payment-1", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://shop.example.com/my-transactions/receipts/tx-1/payment-1\"", sent.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReceiptMessage_ToAGuest_HasNoLinkToAPageTheyCannotSignInTo()
    {
        // Arrange
        var links = new Mock<ITransactionCustomerLinkService>(MockBehavior.Strict);
        var transaction = TransactionCustomerPageServiceTests.CreateInvoicedTransaction();
        transaction.OwnerKind = CustomerOwnerKind.Guest;

        var handler = CreateReceiptHandler(transaction, Mock.Of<INotificationService>(), links.Object);

        // Act
        await handler.PaymentRecordedAsync(new TransactionPaymentRecordedContext(transaction, transaction.Events.Last()), TestContext.Current.CancellationToken);

        // Assert: the strict mock throws if a link is asked for.
        links.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reminder_ToASignedUpCustomer_LinksTheInvoicePageTheSiteChose()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateReminderService(message => sent = message, CreateLinkService(TransactionCustomerLinkTarget.Site), withInvoices: true);
        var transaction = CreateDueTransaction();

        // Act
        var result = await service.SendUpcomingReminderAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        Assert.Contains("https://shop.example.com/my-transactions/invoices/tx-9", sent.TextBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://shop.example.com/my-transactions/invoices/tx-9\"", sent.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("href=\"https://shop.example.com/transactions/pay/token\"", sent.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reminder_ByDefault_LinksTheInvoicePageInTheAdmin()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateReminderService(message => sent = message, CreateLinkService(TransactionCustomerLinkTarget.Admin), withInvoices: true);

        // Act
        await service.SendReminderAsync(CreateDueTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("https://shop.example.com/Admin/transaction-invoices/tx-9", sent.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reminder_WithoutTheReceiptsModule_HasNoInvoiceLink()
    {
        // Arrange
        INotificationMessage sent = null;
        var service = CreateReminderService(message => sent = message, CreateLinkService(TransactionCustomerLinkTarget.Site), withInvoices: false);

        // Act
        await service.SendUpcomingReminderAsync(CreateDueTransaction(), TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("my-transactions", sent.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("my-transactions", sent.HtmlBody, StringComparison.Ordinal);
    }

    private static (string Template, string Name) GetGetRoute(Type controller, string action)
    {
        var attribute = controller.GetMethod(action).GetCustomAttribute<HttpGetAttribute>();

        return (attribute.Template, attribute.Name);
    }

    private static ControllerContext CreateControllerContext(ClaimsPrincipal user)
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = user,
            },
        };

    private static MyTransactionsController CreateSiteController(ITransactionCustomerPageService pages, ClaimsPrincipal user)
        => new(pages, Mock.Of<IShapeFactory>())
        {
            ControllerContext = CreateControllerContext(user),
        };

    private static TransactionCustomerLinkService CreateLinkService(TransactionCustomerLinkTarget target, string adminPrefix = "Admin")
    {
        var site = Mock.Of<ISite>(candidate => candidate.BaseUrl == "https://shop.example.com/");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(candidate => candidate.GetSiteSettingsAsync()).ReturnsAsync(site);

        return new TransactionCustomerLinkService(
            siteService.Object,
            new HttpContextAccessor(),
            Mock.Of<LinkGenerator>(),
            Options.Create(new TransactionCustomerPagesOptions
            {
                LinkTarget = target,
            }),
            Options.Create(new AdminOptions
            {
                AdminUrlPrefix = adminPrefix,
            }));
    }

    private static Transaction CreateDueTransaction()
        => new()
        {
            ItemId = "tx-9",
            Title = "Kitchen remodel — payment 2 of 6",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            TotalAmount = 150m,
            Status = TransactionStatus.Outstanding,
            DueUtc = _now.AddDays(3),
        };

    private static PaymentReceiptTransactionPaymentHandler CreateReceiptHandler(Transaction transaction, INotificationService notifications, ITransactionCustomerLinkService links)
    {
        var userService = new Mock<IUserService>();
        userService.Setup(service => service.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(Mock.Of<IUser>());

        var receipts = new Mock<ITransactionReceiptBuilder>();
        receipts
            .Setup(builder => builder.BuildAsync(It.IsAny<Transaction>(), It.IsAny<TransactionEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction _, TransactionEvent payment, CancellationToken _) => new ReceiptDocument
            {
                Reference = payment.Id,
                Currency = "USD",
                Total = payment.Amount ?? 0m,
                BilledToEmail = "jane@example.com",
            });

        var services = new ServiceCollection()
            .AddSingleton(links)
            .BuildServiceProvider();

        return new PaymentReceiptTransactionPaymentHandler(
            receipts.Object,
            new DefaultReceiptHtmlRenderer(HtmlEncoder.Default, new PassThroughStringLocalizer<DefaultReceiptHtmlRenderer>()),
            TransactionManagerFactory.Create(new FakeTransactionStore(transaction)),
            TestCustomerOwnerNotifier.Create(notifications, userService.Object, services: services),
            services,
            new TestClock(_now),
            NullLogger<PaymentReceiptTransactionPaymentHandler>.Instance,
            new PassThroughStringLocalizer<PaymentReceiptTransactionPaymentHandler>());
    }

    private static DefaultTransactionReminderService CreateReminderService(Action<INotificationMessage> onSend, ITransactionCustomerLinkService links, bool withInvoices)
    {
        var userService = new Mock<IUserService>();
        userService.Setup(candidate => candidate.GetUserByUniqueIdAsync("customer-1")).ReturnsAsync(Mock.Of<IUser>());

        var notifications = new Mock<INotificationService>();
        notifications
            .Setup(candidate => candidate.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object _, INotificationMessage message, CancellationToken _) => onSend(message))
            .ReturnsAsync(new NotificationSendResult { SuccessfulCount = 1 });

        var payLinks = new Mock<ITransactionPayLinkService>();
        payLinks
            .Setup(candidate => candidate.CreatePayUrlAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://shop.example.com/transactions/pay/token");

        var numbers = new Mock<IFinancialDocumentNumberGenerator>();
        numbers
            .Setup(candidate => candidate.GenerateAsync(It.IsAny<FinancialDocumentNumberRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialDocumentNumber(1001, "INV-1001"));

        var services = new ServiceCollection()
            .AddSingleton<ITransactionInvoiceService>(new TransactionInvoiceService(numbers.Object))
            .AddSingleton(payLinks.Object)
            .AddSingleton(links);

        if (withInvoices)
        {
            services.AddSingleton(Mock.Of<ITransactionInvoiceBuilder>());
        }

        return new DefaultTransactionReminderService(
            TestCustomerOwnerNotifier.Create(notifications.Object, userService.Object, Mock.Of<ICustomerContactResolver>(), services.BuildServiceProvider()),
            services.BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<DefaultTransactionReminderService>.Instance,
            new PassThroughStringLocalizer<DefaultTransactionReminderService>());
    }
}
