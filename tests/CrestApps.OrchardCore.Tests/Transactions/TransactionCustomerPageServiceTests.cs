using System.Security.Claims;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The customer pages in the admin and in the site theme build their documents through one service, so these tests pin
/// the ownership rules both apply: a customer sees only their own invoices and receipts, a manager sees any, someone
/// else's transaction is "not found" rather than "forbidden", and the statement only ever lists the viewer's own.
/// </summary>
public sealed class TransactionCustomerPageServiceTests
{
    private const string _payUrl = "https://shop.example.com/transactions/pay/token";

    [Fact]
    public async Task BuildInvoiceAsync_ForTheCustomerWhoOwnsIt_ReturnsTheInvoiceAndItsPayLink()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Found, result.Status);
        Assert.Equal("tx-1", result.Model.Transaction.ItemId);
        Assert.Equal("Invoice", result.Model.Invoice.HeaderTitle);
        Assert.False(result.Model.IsAdministrator);
        Assert.Equal(_payUrl, result.Model.PayUrl);
    }

    [Fact]
    public async Task BuildInvoiceAsync_ForAnotherCustomersTransaction_IsNotFound()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-2"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
        Assert.Null(result.Model);
    }

    [Fact]
    public async Task BuildInvoiceAsync_ForAGuestTransactionThatSharesTheUsersId_IsNotFound()
    {
        // Arrange
        var transaction = CreateInvoicedTransaction();
        transaction.OwnerKind = CustomerOwnerKind.Guest;

        var service = CreateService(new FakeTransactionStore(transaction), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task BuildInvoiceAsync_WithoutEitherPermission_IsForbidden()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()));

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task BuildInvoiceAsync_ForAManager_ShowsAnyCustomersInvoiceAsAnAdministrator()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ManageTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("manager-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Found, result.Status);
        Assert.True(result.Model.IsAdministrator);
    }

    [Fact]
    public async Task BuildInvoiceAsync_BeforeTheCustomerWasSentAnInvoice_IsNotFound()
    {
        // Arrange
        var transaction = CreateInvoicedTransaction();
        transaction.InvoiceNumber = null;

        var service = CreateService(new FakeTransactionStore(transaction), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task BuildInvoiceAsync_WithoutTheReceiptsModule_IsNotFound()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), withDocumentBuilders: false, TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task BuildInvoiceAsync_ForAPaidInvoice_HasNoPayLink()
    {
        // Arrange
        var transaction = CreateInvoicedTransaction();
        transaction.AmountPaid = transaction.TotalAmount;
        transaction.Status = TransactionStatus.Paid;

        var service = CreateService(new FakeTransactionStore(transaction), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildInvoiceAsync(CreateUser("customer-1"), "tx-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Found, result.Status);
        Assert.Null(result.Model.PayUrl);
    }

    [Fact]
    public async Task BuildReceiptAsync_ForTheCustomerWhoOwnsIt_ReturnsTheReceiptOfThatPayment()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildReceiptAsync(CreateUser("customer-1"), "tx-1", "payment-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Found, result.Status);
        Assert.Equal("payment-1", result.Model.Payment.Id);
        Assert.Equal(40m, result.Model.Receipt.Total);
        Assert.False(result.Model.IsAdministrator);
    }

    [Fact]
    public async Task BuildReceiptAsync_ForAnotherCustomersTransaction_IsNotFound()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildReceiptAsync(CreateUser("customer-2"), "tx-1", "payment-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
    }

    [Theory]
    [InlineData("payment-unknown")]
    [InlineData("note-1")]
    public async Task BuildReceiptAsync_ForAnEventThatIsNotAPayment_IsNotFound(string paymentId)
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildReceiptAsync(CreateUser("customer-1"), "tx-1", paymentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task BuildReceiptAsync_WithoutEitherPermission_IsForbidden()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()));

        // Act
        var result = await service.BuildReceiptAsync(CreateUser("customer-1"), "tx-1", "payment-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task BuildStatementAsync_ListsOnlyTheViewersOwnTransactions_WithTheirDocumentsAndPayLinks()
    {
        // Arrange
        var someoneElses = CreateInvoicedTransaction();
        someoneElses.ItemId = "tx-2";
        someoneElses.OwnerId = "customer-2";

        var guest = CreateInvoicedTransaction();
        guest.ItemId = "tx-3";
        guest.OwnerKind = CustomerOwnerKind.Guest;

        var store = new FakeTransactionStore(CreateInvoicedTransaction(), someoneElses, guest);
        var service = CreateService(store, TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildStatementAsync(CreateUser("customer-1"), new TransactionStatementRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Found, result.Status);
        Assert.Equal(1, result.Model.TotalCount);

        var entry = Assert.Single(result.Model.Entries);

        Assert.Equal("tx-1", entry.Transaction.ItemId);
        Assert.Equal(_payUrl, entry.PayUrl);
        Assert.Equal("/my-transactions/invoices/tx-1", entry.InvoiceUrl);

        var receipt = Assert.Single(entry.Receipts);

        Assert.Equal("payment-1", receipt.Payment.Id);
        Assert.Equal("/my-transactions/receipts/tx-1/payment-1", receipt.Url);
    }

    [Fact]
    public async Task BuildStatementAsync_WithoutThePermissionToViewOwnTransactions_IsForbidden()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ManageTransactions);

        // Act
        var result = await service.BuildStatementAsync(CreateUser("customer-1"), new TransactionStatementRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task BuildStatementAsync_ForAUserWithoutAnIdentifier_IsForbidden()
    {
        // Arrange
        var service = CreateService(new FakeTransactionStore(CreateInvoicedTransaction()), TransactionsPermissions.ViewOwnTransactions);

        // Act
        var result = await service.BuildStatementAsync(new ClaimsPrincipal(new ClaimsIdentity()), new TransactionStatementRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionCustomerPageStatus.Forbidden, result.Status);
    }

    internal static ClaimsPrincipal CreateUser(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    internal static Transaction CreateInvoicedTransaction()
        => new()
        {
            ItemId = "tx-1",
            Title = "Kitchen remodel",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            AmountPaid = 40m,
            Status = TransactionStatus.PartiallyPaid,
            InvoiceNumber = "INV-1001",
            Events =
            [
                new TransactionEvent
                {
                    Id = "note-1",
                    Type = TransactionEventType.Note,
                    Message = "Called the customer.",
                },
                new TransactionEvent
                {
                    Id = "payment-1",
                    Type = TransactionEventType.PaymentRecorded,
                    Amount = 40m,
                    Method = TransactionsConstants.SettlementMethods.Offline,
                    Message = "Paid by check.",
                },
            ],
        };

    private static TransactionCustomerPageService CreateService(FakeTransactionStore store, params Permission[] granted)
        => CreateService(store, withDocumentBuilders: true, granted);

    private static TransactionCustomerPageService CreateService(FakeTransactionStore store, bool withDocumentBuilders, params Permission[] granted)
    {
        var services = new ServiceCollection();

        if (withDocumentBuilders)
        {
            var invoices = new Mock<ITransactionInvoiceBuilder>();
            invoices
                .Setup(builder => builder.BuildAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Transaction transaction, CancellationToken _) => new ReceiptDocument
                {
                    HeaderTitle = "Invoice",
                    Reference = transaction.InvoiceNumber,
                    Total = transaction.TotalAmount,
                });

            var receipts = new Mock<ITransactionReceiptBuilder>();
            receipts
                .Setup(builder => builder.BuildAsync(It.IsAny<Transaction>(), It.IsAny<TransactionEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Transaction _, TransactionEvent payment, CancellationToken _) => new ReceiptDocument
                {
                    Reference = payment.Id,
                    Total = payment.Amount ?? 0m,
                });

            services.AddSingleton(invoices.Object);
            services.AddSingleton(receipts.Object);
        }

        var payLinks = new Mock<ITransactionPayLinkService>();
        payLinks
            .Setup(links => links.CreatePayUrlAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_payUrl);

        var linkGenerator = new Mock<LinkGenerator>();
        linkGenerator
            .Setup(generator => generator.GetPathByAddress(
                It.IsAny<HttpContext>(),
                It.IsAny<RouteValuesAddress>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<RouteValueDictionary>(),
                It.IsAny<PathString?>(),
                It.IsAny<FragmentString>(),
                It.IsAny<LinkOptions>()))
            .Returns((HttpContext _, RouteValuesAddress address, RouteValueDictionary values, RouteValueDictionary _, PathString? _, FragmentString _, LinkOptions _) =>
                address.RouteName switch
                {
                    TransactionsConstants.RouteNames.Invoice => $"/my-transactions/invoices/{values["itemId"]}",
                    TransactionsConstants.RouteNames.Receipt => $"/my-transactions/receipts/{values["itemId"]}/{values["paymentId"]}",
                    _ => null,
                });

        return new TransactionCustomerPageService(
            TransactionManagerFactory.Create(store),
            new GrantingAuthorizationService(granted),
            payLinks.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            linkGenerator.Object,
            services.BuildServiceProvider());
    }

    /// <summary>
    /// Grants exactly the permissions it was given.
    /// </summary>
    internal sealed class GrantingAuthorizationService : IAuthorizationService
    {
        private readonly HashSet<string> _granted;

        public GrantingAuthorizationService(IEnumerable<Permission> granted)
        {
            _granted = granted.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var granted = requirements.OfType<PermissionRequirement>().All(requirement => _granted.Contains(requirement.Permission.Name));

            return Task.FromResult(granted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
