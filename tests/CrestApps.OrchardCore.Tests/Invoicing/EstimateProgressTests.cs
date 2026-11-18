using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// Progress invoicing bills a percent of each estimate line, never more than all of it across the invoices that are not
/// void; a void invoice gives its share back; the estimate is invoiced once every line is; and once part is invoiced the
/// whole estimate can't be invoiced again in one go.
/// </summary>
public sealed class EstimateProgressTests
{
    [Fact]
    public async Task ShareOfEachLine_IsInvoiced_NeverPastAllOfIt_ThenTheEstimateIsInvoiced()
    {
        // Arrange: design 10 hours at $100 and build 1 at $2,000.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);

        // Act
        var first = await ProgressAsync(harness, estimate.ItemId, new() { ["design"] = 30m, ["build"] = 30m });
        var tooMuch = await ProgressAsync(harness, estimate.ItemId, new() { ["build"] = 80m });
        var whole = await RunAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Full, TestContext.Current.CancellationToken));
        var rest = await ProgressAsync(harness, estimate.ItemId, new() { ["design"] = 70m, ["build"] = 70m });

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        var invoice = await harness.FindInvoiceAsync(first.Invoice.ItemId);
        Assert.Equal([("Design (30%)", 3m, 100m), ("Build (30%)", 0.3m, 2000m)], invoice.Lines.Select(line => (line.Name, line.Quantity, line.UnitPrice)));
        Assert.Equal(900m, invoice.Totals.Total);

        Assert.False(tooMuch.Succeeded);
        Assert.False(whole.Succeeded);
        Assert.True(rest.Succeeded, string.Join(" ", rest.Errors));
        Assert.Equal(2100m, (await harness.FindInvoiceAsync(rest.Invoice.ItemId)).Totals.Total);

        var percents = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().GetInvoicedPercentsAsync(estimate.ItemId, TestContext.Current.CancellationToken));
        Assert.Equal((100m, 100m), (percents["design"], percents["build"]));
        Assert.Equal(EstimateStatus.Invoiced, (await FindAsync(harness, estimate.ItemId)).Status);
    }

    [Fact]
    public async Task AVoidInvoice_GivesItsShareBack()
    {
        // Arrange: 50% of the build invoiced, issued, then voided.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);
        var half = await ProgressAsync(harness, estimate.ItemId, new() { ["build"] = 50m });
        var issued = await harness.RunAsync(commands => commands.IssueAsync(half.Invoice.ItemId, TestContext.Current.CancellationToken));
        var voided = await harness.RunAsync(commands => commands.VoidAsync(half.Invoice.ItemId, "Billed too early", TestContext.Current.CancellationToken));

        // Act
        var percents = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().GetInvoicedPercentsAsync(estimate.ItemId, TestContext.Current.CancellationToken));
        var again = await ProgressAsync(harness, estimate.ItemId, new() { ["build"] = 100m });

        // Assert
        Assert.True(issued.Succeeded && voided.Succeeded, string.Join(" ", issued.Errors.Concat(voided.Errors)));
        Assert.False(percents.ContainsKey("build"));
        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
    }

    private static Task<EstimateCommandResult> ProgressAsync(InvoicingTestHarness harness, string estimateId, Dictionary<string, decimal> percents)
        => RunAsync(harness, commands => commands.InvoiceProgressAsync(estimateId, percents, TestContext.Current.CancellationToken));

    private static Task<EstimateCommandResult> RunAsync(InvoicingTestHarness harness, Func<IEstimateCommandService, Task<EstimateCommandResult>> command)
        => harness.InScopeAsync(services => command(services.GetRequiredService<IEstimateCommandService>()));

    private static Task<Estimate> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IEstimateManager>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private static async Task<Estimate> SendAsync(InvoicingTestHarness harness)
    {
        var estimate = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IEstimateManager>();
            var draft = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            draft.Currency = "USD";
            draft.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            draft.Lines.Add(new DocumentLine { Id = "design", Name = "Design", Quantity = 10m, UnitPrice = 100m });
            draft.Lines.Add(new DocumentLine { Id = "build", Name = "Build", Quantity = 1m, UnitPrice = 2000m });
            await manager.CreateAsync(draft, TestContext.Current.CancellationToken);

            return draft;
        });

        var sent = await RunAsync(harness, commands => commands.SendAsync(estimate.ItemId, TestContext.Current.CancellationToken));

        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));

        return sent.Estimate;
    }
}
