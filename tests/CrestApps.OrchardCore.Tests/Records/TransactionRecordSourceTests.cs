using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Transactions;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Transactions, such as invoices, are a record kind: a user who manages transactions finds them by title, and any
/// record that links to one shows its invoice number, title, status and amount.
/// </summary>
public sealed class TransactionRecordSourceTests
{
    [Fact]
    public async Task ASearch_FindsTransactionsByTitle_ForAUserWhoManagesTransactions()
    {
        // Arrange
        var source = CreateSource();
        var manager = PermissionClaimAuthorizationService.User(TransactionsPermissions.ManageTransactions);
        var customer = PermissionClaimAuthorizationService.User(TransactionsPermissions.ViewOwnTransactions);

        // Act
        var found = await source.SearchAsync(new RecordSearch { Text = " plan ", User = manager }, TestContext.Current.CancellationToken);
        var capped = await source.SearchAsync(new RecordSearch { User = manager, Count = 1 }, TestContext.Current.CancellationToken);
        var forbidden = await source.SearchAsync(new RecordSearch { Text = "plan", User = customer }, TestContext.Current.CancellationToken);

        // Assert
        var invoice = Assert.Single(found);

        Assert.Equal("t1", invoice.Id);
        Assert.Equal("INV-1001 Annual plan", invoice.Title);
        Assert.Equal("Outstanding, USD 120.00", invoice.Description);
        Assert.Equal("/Admin/transactions/Detail/t1", invoice.AdminUrl);
        Assert.Single(capped);
        Assert.Empty(forbidden);
        Assert.False(await source.CanSearchAsync(customer, TestContext.Current.CancellationToken));
        Assert.False(await source.CanSearchAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSummaries_DescribeTheTransactionsFound_ByTheirIds()
    {
        // Arrange
        var source = CreateSource();

        // Act
        var summaries = await source.GetSummariesAsync(["t2", "missing"], TestContext.Current.CancellationToken);
        var nothing = await source.GetSummariesAsync([], TestContext.Current.CancellationToken);

        // Assert
        var summary = Assert.Single(summaries);

        Assert.Equal("t2", summary.Key);
        Assert.Equal("Setup fee", summary.Value.Title);
        Assert.Equal("Paid, JPY 5000", summary.Value.Description);
        Assert.Empty(nothing);
        Assert.Equal(TransactionsConstants.RecordKind, source.Kind);
    }

    private static TransactionRecordSource CreateSource()
    {
        var store = new FakeTransactionStore(
            new Transaction
            {
                ItemId = "t1",
                Title = "Annual plan",
                InvoiceNumber = "INV-1001",
                Status = TransactionStatus.Outstanding,
                TotalAmount = 120m,
                Currency = "USD",
            },
            new Transaction
            {
                ItemId = "t2",
                Title = "Setup fee",
                Status = TransactionStatus.Paid,
                TotalAmount = 5000m,
                Currency = "JPY",
            });

        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        return new TransactionRecordSource(
            store,
            new PermissionClaimAuthorizationService(),
            new TemplateLinkGenerator((_, values) => $"/Admin/transactions/{values["action"]}/{values["itemId"]}"),
            httpContextAccessor.Object,
            new StringLocalizer<TransactionRecordSource>(new SourceTextLocalizerFactory()));
    }
}
