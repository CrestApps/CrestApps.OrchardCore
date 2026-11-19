using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Moq;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// The payment request of a transaction (its invoice to the customer) is a business document of its own kind, with its
/// own template: it shows each adjustment still in force, a tax line per rate, what is still owed, and is addressed to
/// the payer.
/// </summary>
public sealed class TransactionInvoiceBuilderTests
{
    private static readonly DateTime _now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Build_MakesAPaymentRequest_WithEachAdjustmentStillInForce_AndWhatIsOwed()
    {
        // Arrange: a waiver in force and a discount that was taken back.
        var transaction = CreateTransaction();
        transaction.AmountAdjusted = 30m;
        transaction.Events.Add(new TransactionEvent { Id = "adj-1", Type = TransactionEventType.Adjusted, Amount = 30m, AdjustmentKind = TransactionAdjustmentKind.Waiver, Reason = "Financial aid" });
        transaction.Events.Add(new TransactionEvent { Id = "adj-2", Type = TransactionEventType.Adjusted, Amount = 5m, AdjustmentKind = TransactionAdjustmentKind.Discount, Reason = "Typo" });
        transaction.Events.Add(new TransactionEvent { Id = "rev-1", Type = TransactionEventType.Reversed, Amount = 5m, ReversesEventId = "adj-2" });

        // Act
        var document = await CreateBuilder().BuildAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TransactionsConstants.BusinessDocumentKinds.PaymentRequest, document.Kind);
        Assert.Equal("Invoice", document.Title);
        Assert.Equal("INV-1001", document.Number);
        Assert.DoesNotContain(document.Fields, field => field.Value == "INV-1001");
        Assert.Contains(document.Rows, row => row.Cells.Values.Any(cell => cell?.Contains("Financial aid", StringComparison.Ordinal) == true));
        Assert.DoesNotContain(document.Rows, row => row.Cells.Values.Any(cell => cell?.Contains("Typo", StringComparison.Ordinal) == true));
        Assert.Equal("$70.00", document.Totals[^1].Value);
        Assert.Equal("Due", document.Status.Label);
    }

    [Fact]
    public async Task Build_ShowsATaxLinePerRate_AndIsAddressedToThePayer()
    {
        // Arrange
        var transaction = CreateTransaction();
        transaction.TotalAmount = 113m;
        transaction.TaxAmount = 13m;
        transaction.TaxLines =
        [
            new TaxLine { TaxName = "GST", Rate = 0.05m, TaxAmount = 5m },
            new TaxLine { TaxName = "PST", Rate = 0.08m, TaxAmount = 8m },
        ];
        transaction.PayerContact = new ContactInfo { Name = "Contoso Ltd.", Email = "ap@contoso.example" };

        // Act
        var document = await CreateBuilder().BuildAsync(transaction, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(document.Totals, total => total.Label == "GST (5%)" && total.Value == "$5.00");
        Assert.Contains(document.Totals, total => total.Label == "PST (8%)" && total.Value == "$8.00");
        Assert.Equal("Contoso Ltd.", document.Recipient.Name);
        Assert.Equal("ap@contoso.example", document.Recipient.Email);
    }

    [Fact]
    public async Task Build_AppliesThePaymentRequestTemplate_OnlyOnceBusinessDocumentsIsEnabled()
    {
        // Arrange
        var templates = new Mock<IBusinessDocumentTemplateManager>(MockBehavior.Strict);
        templates
            .Setup(manager => manager.ApplyAsync(It.IsAny<BusinessDocument>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await CreateBuilder(templates.Object, ReceiptRenderers.CreateFeatures()).BuildAsync(CreateTransaction(), TestContext.Current.CancellationToken);
        templates.VerifyNoOtherCalls();

        await CreateBuilder(templates.Object, ReceiptRenderers.CreateFeatures(BusinessDocumentsConstants.Features.Area)).BuildAsync(CreateTransaction(), TestContext.Current.CancellationToken);

        // Assert
        templates.Verify(manager => manager.ApplyAsync(It.Is<BusinessDocument>(document => document.Kind == TransactionsConstants.BusinessDocumentKinds.PaymentRequest), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Transaction CreateTransaction()
        => new()
        {
            ItemId = "transaction-1",
            OwnerId = "customer-1",
            OwnerKind = CustomerOwnerKind.Authenticated,
            Title = "Fall registration",
            Currency = "USD",
            Amount = 100m,
            TotalAmount = 100m,
            InvoiceNumber = "INV-1001",
            Status = TransactionStatus.Outstanding,
            CreatedUtc = _now.AddDays(-10),
            UpdatedUtc = _now.AddDays(-10),
        };

    private static TransactionInvoiceBuilder CreateBuilder(IBusinessDocumentTemplateManager templates = null, IShellFeaturesManager features = null)
        => new(
            CreateReceiptService(),
            ReceiptRenderers.CreateMapper(),
            templates ?? Mock.Of<IBusinessDocumentTemplateManager>(),
            features ?? ReceiptRenderers.CreateFeatures(),
            Mock.Of<ICustomerContactResolver>(),
            null,
            new PassThroughStringLocalizer<TransactionInvoiceBuilder>());

    private static IReceiptService CreateReceiptService()
    {
        var receipts = new Mock<IReceiptService>();
        receipts
            .Setup(service => service.BuildAsync(It.IsAny<ReceiptRequest>()))
            .ReturnsAsync((ReceiptRequest request) => new ReceiptDocument
            {
                Reference = request.Reference,
                SourceLabel = request.SourceLabel,
                BilledToName = request.BilledToName,
                BilledToEmail = request.BilledToEmail,
                Currency = request.Currency,
                LineItems = request.LineItems,
                TaxLines = request.TaxLines,
                TaxAmount = request.TaxAmount,
                Total = request.Total,
                Subtotal = request.Total - request.TaxAmount,
                Status = request.Status,
                Notes = request.Notes,
            });

        return receipts.Object;
    }
}
