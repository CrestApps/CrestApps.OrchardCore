using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An estimate is numbered when it is sent and locked after, accepted or declined by the customer while it is valid,
/// revised under its number, expired by the sweep, and turned into a draft invoice: the whole of it, or its deposit as a
/// prepayment invoice, once each.
/// </summary>
public sealed class EstimateTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    [Fact]
    public async Task Sending_NumbersTheEstimate_SetsItsExpiry_AndLocksIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await CreateAsync(harness);

        // Act
        var sent = await RunAsync(harness, commands => commands.SendAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var again = await RunAsync(harness, commands => commands.SendAsync(draft.ItemId, TestContext.Current.CancellationToken));
        var change = await harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IEstimateManager>();
            var stored = await manager.FindByIdAsync(draft.ItemId, TestContext.Current.CancellationToken);
            stored.Notes = "Changed";

            return await manager.ValidateAsync(stored, TestContext.Current.CancellationToken);
        });

        // Assert
        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));
        Assert.Equal(("EST-1001", EstimateStatus.Sent, _today.AddDays(30), 100m), (sent.Estimate.Number, sent.Estimate.Status, sent.Estimate.ExpiryDate, sent.Estimate.Totals.Total));
        Assert.True(again.AlreadyDone);
        Assert.False(change.Succeeded);
        Assert.Contains("EstimateSent:EST-1001", harness.Observed);
    }

    [Fact]
    public async Task Accepting_RecordsWhoAccepted_AndInvoicesTheWholeEstimate_WhenAskedTo()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness, await CreateAsync(harness, draft => draft.OnAcceptance = EstimateConversion.Full));

        // Act
        var nameless = await RunAsync(harness, commands => commands.AcceptAsync(estimate.ItemId, new EstimateAcceptanceRequest { Today = _today }, TestContext.Current.CancellationToken));
        var accepted = await RunAsync(harness, commands => commands.AcceptAsync(estimate.ItemId, new EstimateAcceptanceRequest { Name = "Pat Doe", IpAddress = "203.0.113.9", Today = _today }, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(nameless.Succeeded);
        Assert.True(accepted.Succeeded, string.Join(" ", accepted.Errors));
        Assert.Equal((EstimateStatus.Invoiced, "Pat Doe", "203.0.113.9"), (accepted.Estimate.Status, accepted.Estimate.Acceptance.Name, accepted.Estimate.Acceptance.IpAddress));

        var invoice = await harness.FindInvoiceAsync(accepted.Invoice.ItemId);
        Assert.Equal((InvoiceStatus.Draft, InvoiceKind.Standard, InvoicingConstants.EstimateReferenceType, estimate.ItemId, "EST-1001", 100m), (invoice.Status, invoice.Kind, invoice.SourceType, invoice.SourceId, invoice.Reference, invoice.Totals.Total));
        Assert.Equal("Consulting", Assert.Single(invoice.Lines).Name);
        Assert.Equal(["EstimateSent:EST-1001", "EstimateAccepted:EST-1001", "EstimateConverted:EST-1001"], harness.Observed.Where(entry => entry.StartsWith("Estimate", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AnExpiredEstimate_CantBeAccepted_AndTheSweepExpiresIt()
    {
        // Arrange: valid until yesterday, and one valid next week.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var late = await SendAsync(harness, await CreateAsync(harness, draft => draft.ExpiryDate = _today.AddDays(-1)));
        var valid = await SendAsync(harness, await CreateAsync(harness, draft => draft.ExpiryDate = _today.AddDays(7)));

        // Act
        var accepted = await RunAsync(harness, commands => commands.AcceptAsync(late.ItemId, new EstimateAcceptanceRequest { Name = "Pat", Today = _today }, TestContext.Current.CancellationToken));
        var expired = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().ExpireDueAsync(_today, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(accepted.Succeeded);
        Assert.Equal(1, expired);
        Assert.Equal(EstimateStatus.Expired, (await FindAsync(harness, late.ItemId)).Status);
        Assert.Equal(EstimateStatus.Sent, (await FindAsync(harness, valid.ItemId)).Status);
        Assert.Contains($"EstimateExpired:{late.Number}", harness.Observed);
    }

    [Fact]
    public async Task ADeclinedEstimate_IsRevised_UnderItsNumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness, await CreateAsync(harness));

        // Act
        var declined = await RunAsync(harness, commands => commands.DeclineAsync(estimate.ItemId, "Too expensive", TestContext.Current.CancellationToken));
        var revised = await RunAsync(harness, commands => commands.ReviseAsync(estimate.ItemId, TestContext.Current.CancellationToken));
        var sent = await RunAsync(harness, commands => commands.SendAsync(revised.Estimate.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(declined.Succeeded, string.Join(" ", declined.Errors));
        Assert.True(revised.Succeeded, string.Join(" ", revised.Errors));
        Assert.Equal((2, estimate.ItemId, EstimateStatus.Draft), (revised.Estimate.Revision, revised.Estimate.RevisionOfId, revised.Estimate.Status));

        var original = await FindAsync(harness, estimate.ItemId);
        Assert.Equal((EstimateStatus.Canceled, revised.Estimate.ItemId, "Too expensive"), (original.Status, original.ReplacedById, original.DeclineReason));
        Assert.Equal("EST-1001-2", sent.Estimate.Number);
    }

    [Fact]
    public async Task TheDeposit_IsInvoicedOnce_ThenTheWork()
    {
        // Arrange: 30% of 100 asked as a deposit.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness, await CreateAsync(harness, draft => draft.DepositPercent = 30m));

        // Act
        var deposit = await RunAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Deposit, TestContext.Current.CancellationToken));
        var depositAgain = await RunAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Deposit, TestContext.Current.CancellationToken));
        var work = await RunAsync(harness, commands => commands.ConvertAsync(estimate.ItemId, EstimateConversion.Full, TestContext.Current.CancellationToken));
        var cancel = await RunAsync(harness, commands => commands.CancelAsync(estimate.ItemId, "No", TestContext.Current.CancellationToken));

        // Assert
        Assert.True(deposit.Succeeded, string.Join(" ", deposit.Errors));
        var prepayment = await harness.FindInvoiceAsync(deposit.Invoice.ItemId);
        Assert.Equal((InvoiceKind.Prepayment, 30m, DocumentLineKind.Deposit), (prepayment.Kind, prepayment.Totals.Total, Assert.Single(prepayment.Lines).Kind));
        Assert.True(depositAgain.AlreadyDone);
        Assert.Equal(deposit.Invoice.ItemId, depositAgain.Invoice.ItemId);

        Assert.True(work.Succeeded, string.Join(" ", work.Errors));
        Assert.Equal(InvoiceKind.Standard, (await harness.FindInvoiceAsync(work.Invoice.ItemId)).Kind);

        var stored = await FindAsync(harness, estimate.ItemId);
        Assert.Equal((EstimateStatus.Invoiced, 2), (stored.Status, stored.InvoiceIds.Count));
        Assert.False(cancel.Succeeded);
    }

    [Fact]
    public async Task TheDocument_IsAnEstimate_ValidUntilItsExpiry()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness, await CreateAsync(harness, draft => draft.DepositAmount = 25m));

        // Act
        var document = await harness.InScopeAsync(services => services.GetRequiredService<IInvoiceDocumentBuilder>().BuildAsync(estimate, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal((InvoicingConstants.BusinessDocumentKinds.Estimate, "Estimate", "EST-1001"), (document.Kind, document.Title, document.Number));
        Assert.Contains(document.Fields, field => field.Label == "Valid until");
        Assert.Contains(document.Sections, section => section.Title == "Deposit");
        Assert.Null(document.Watermark);
    }

    [Fact]
    public async Task ASentEstimate_ImportsWithItsNumber_AndASecondImportKeepsIt()
    {
        // Arrange
        await using var source = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(source, await CreateAsync(source));
        var json = CrestApps.OrchardCore.Invoicing.Deployments.InvoicingDocumentExport.Export(estimate);

        await using var target = await InvoicingTestHarness.CreateAsync();

        // Act
        var (first, second) = await target.InScopeAsync(async services =>
        {
            var importer = services.GetRequiredService<InvoicingDocumentImporter>();
            var store = services.GetRequiredService<IEstimateStore>();

            return (
                await importer.ImportEstimatesAsync([(System.Text.Json.Nodes.JsonObject)json.DeepClone()], store, TestContext.Current.CancellationToken),
                await importer.ImportEstimatesAsync([(System.Text.Json.Nodes.JsonObject)json.DeepClone()], store, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal((1, 0), (first.Imported, first.Errors.Count));
        Assert.Equal(1, second.Kept + second.Imported);
        Assert.Equal(("EST-1001", EstimateStatus.Sent), ((await FindAsync(target, estimate.ItemId)).Number, (await FindAsync(target, estimate.ItemId)).Status));
    }

    private static Task<EstimateCommandResult> RunAsync(InvoicingTestHarness harness, Func<IEstimateCommandService, Task<EstimateCommandResult>> command)
        => harness.InScopeAsync(services => command(services.GetRequiredService<IEstimateCommandService>()));

    private static Task<Estimate> FindAsync(InvoicingTestHarness harness, string id)
        => harness.InScopeAsync(services => services.GetRequiredService<IEstimateStore>().FindByIdAsync(id, TestContext.Current.CancellationToken).AsTask());

    private static async Task<Estimate> SendAsync(InvoicingTestHarness harness, Estimate draft)
    {
        var sent = await RunAsync(harness, commands => commands.SendAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));

        return sent.Estimate;
    }

    private static Task<Estimate> CreateAsync(InvoicingTestHarness harness, Action<Estimate> configure = null)
        => harness.InScopeAsync(async services =>
        {
            var manager = services.GetRequiredService<IEstimateManager>();
            var estimate = await manager.NewAsync(cancellationToken: TestContext.Current.CancellationToken);

            estimate.Currency = "USD";
            estimate.BillTo = new DocumentParty { Contact = new ContactInfo { Name = "Fabrikam Ltd.", Email = "ap@fabrikam.example" } };
            estimate.Lines.Add(new DocumentLine { Id = "line-1", Name = "Consulting", Quantity = 2m, UnitPrice = 50m });

            configure?.Invoke(estimate);

            var validation = await manager.ValidateAsync(estimate, TestContext.Current.CancellationToken);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(estimate, TestContext.Current.CancellationToken);

            return estimate;
        });
}
