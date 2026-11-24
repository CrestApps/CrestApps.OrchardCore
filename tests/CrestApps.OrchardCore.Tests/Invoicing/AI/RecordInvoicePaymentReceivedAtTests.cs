using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>previewInvoiceAction</c> and <c>recordInvoicePayment</c> take when the money was received, entered in the site's
/// time zone as the transaction's Record payment form takes it, record it on the ledger in UTC, refuse a time in the
/// future, and bind it into the preview's token, so a payment previewed without it is not recorded with it.
/// </summary>
public sealed class RecordInvoicePaymentReceivedAtTests
{
    private readonly InvoicingLifecycleToolContext _context = new();

    [Fact]
    public async Task RecordInvoicePayment_RecordsWhenTheMoneyWasReceived_EnteredInTheSitesTimeZone()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, "2026-11-10T09:30"), services: NewYork()));

        // Act
        var answer = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Record(invoice, preview.GetProperty("previewToken").GetString(), "2026-11-10T09:30"), services: NewYork()));

        // Assert
        var transaction = await harness.FindTransactionAsync(invoice.TransactionId);
        var payment = Assert.Single(transaction.Events, entry => entry.Type == TransactionEventType.PaymentRecorded);

        Assert.Equal(new DateTime(2026, 11, 10, 14, 30, 0, DateTimeKind.Utc), payment.ReceivedUtc);
        Assert.Equal(40m, answer.GetProperty("recorded").GetDecimal());
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("receivedAt").GetString()));
        Assert.False(string.IsNullOrEmpty(preview.GetProperty("receivedAt").GetString()));
    }

    [Fact]
    public async Task PreviewInvoiceAction_RefusesAReceivedTimeInTheFuture_OrNotATime()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);

        // Act: the harness's now is 15:00 UTC on 13 November, 10:00 in New York.
        var future = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, "2026-11-13T11:00"), services: NewYork());
        var notATime = await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, "last Tuesday"), services: NewYork());

        // Assert
        Assert.Equal("Nothing was previewed: the time the payment was received can't be in the future.", future);
        Assert.Equal("Nothing was previewed: give receivedAt as yyyy-MM-ddTHH:mm in the site's time zone.", notATime);
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    [Fact]
    public async Task RecordInvoicePayment_RefusesAToken_PreviewedWithoutTheReceivedTime()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var invoice = await InvoicingLifecycleToolContext.IssueInvoiceAsync(harness);
        var preview = InvoicingLifecycleToolContext.Parse(await _context.InvokeAsync(harness, new PreviewInvoiceActionTool(), Payment(invoice, null), services: NewYork()));

        // Act
        var answer = await _context.InvokeAsync(harness, new RecordInvoicePaymentTool(), Record(invoice, preview.GetProperty("previewToken").GetString(), "2026-11-10T09:30"), services: NewYork());

        // Assert
        Assert.Contains("issued for other arguments", answer, StringComparison.Ordinal);
        Assert.Equal(100m, (await harness.FindTransactionAsync(invoice.TransactionId)).OutstandingAmount);
    }

    private static Dictionary<string, object> Payment(Invoice invoice, string receivedAt)
    {
        var arguments = PreviewInvoiceActionToolTests.Payment(invoice, 40m);

        if (receivedAt is not null)
        {
            arguments["receivedAt"] = receivedAt;
        }

        return arguments;
    }

    private static Dictionary<string, object> Record(Invoice invoice, string token, string receivedAt)
        => new()
        {
            ["invoice"] = invoice.Number,
            ["amount"] = 40m,
            ["currency"] = "USD",
            ["entireBalance"] = false,
            ["method"] = "check",
            ["reference"] = "chk-4471",
            ["receivedAt"] = receivedAt,
            ["previewToken"] = token,
            ["idempotencyKey"] = "payment-received-1",
        };

    // The site is in New York; times are shown as they are, which is enough to see that one is returned.
    private static Dictionary<Type, object> NewYork()
    {
        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.GetLocalTimeZoneAsync()).ReturnsAsync(Mock.Of<ITimeZone>(zone => zone.TimeZoneId == "America/New_York"));
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);

        return new Dictionary<Type, object> { [typeof(ILocalClock)] = localClock.Object };
    }
}
