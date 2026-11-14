using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A draft saved in the editor shows the totals it will be issued with: the editor and issue work them out the same way.
/// </summary>
public sealed class InvoiceDraftCalculationTests
{
    [Fact]
    public async Task ADraftsTotals_AreTheTotalsItIsIssuedWith()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { CashRoundingIncrement = 0.05m });

        var draft = await harness.CreateDraftAsync(invoice =>
        {
            invoice.Lines.Add(new DocumentLine { Id = "line-2", Name = "Parts", Quantity = 3m, UnitPrice = 9.99m, DiscountPercent = 10m });
            invoice.Lines.Add(new DocumentLine { Id = "line-3", Kind = DocumentLineKind.Text, Name = "Thank you" });
        });

        // Act: what the editor does on save.
        var preview = await harness.InScopeAsync(async services =>
        {
            var invoice = await services.GetRequiredService<IInvoiceManager>().FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken);

            await InvoiceCalculation.ApplyAsync(services.GetRequiredService<IDocumentCalculator>(), invoice, new InvoicingSettings { CashRoundingIncrement = 0.05m }, issueDate: null, TestContext.Current.CancellationToken);
            await services.GetRequiredService<IInvoiceManager>().UpdateAsync(invoice, data: null, TestContext.Current.CancellationToken);

            return invoice.Totals;
        });

        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert: 100 + 26.97 (3 x 9.99 less 10%), rounded to 0.05.
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(126.97m, preview.Subtotal);
        Assert.Equal(126.95m, preview.Total);
        Assert.Equal(preview.Total, issued.Invoice.Totals.Total);
        Assert.Equal(preview.Subtotal, issued.Invoice.Totals.Subtotal);
        Assert.Equal(preview.RoundingAmount, issued.Invoice.Totals.RoundingAmount);
    }
}
