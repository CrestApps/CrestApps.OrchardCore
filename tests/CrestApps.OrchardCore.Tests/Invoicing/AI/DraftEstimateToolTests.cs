using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>draftEstimate</c> saves a draft estimate through the editor of the New estimate page, with its lines, deposit and
/// what accepting it invoices; it never sends it, refuses a caller without the permission, and refuses a customer that
/// does not exist.
/// </summary>
public sealed class DraftEstimateToolTests
{
    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Draft_IsRefused_WithoutThePermission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var answer = await _context.InvokeAsync(harness, new DraftEstimateTool(), Training(), InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageEstimates));

        // Assert
        Assert.Contains("not allowed", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Draft_SavesADraftWithItsLinesAndDeposit()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();

        // Act
        var answer = await _context.InvokeAsync(harness, new DraftEstimateTool(), Training());

        // Assert
        using var json = JsonDocument.Parse(answer);
        Assert.Equal(("draft", 60m), (json.RootElement.GetProperty("status").GetString(), json.RootElement.GetProperty("total").GetDecimal()));

        var estimate = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateStore>().FindByIdAsync(json.RootElement.GetProperty("id").GetString(), TestContext.Current.CancellationToken).AsTask());
        Assert.Equal((EstimateStatus.Draft, "USD", (decimal?)30m, EstimateConversion.Full), (estimate.Status, estimate.Currency, estimate.DepositPercent, estimate.OnAcceptance));
        Assert.Equal(("Contoso Ltd.", "ap@contoso.example"), (estimate.BillTo.Contact.Name, estimate.BillTo.Contact.Email));
        Assert.Equal(["Training"], estimate.Lines.Select(line => line.Name));
        Assert.Null(estimate.Number);
        Assert.Empty(harness.Emails);
    }

    [Fact]
    public async Task Draft_ForACustomerThatDoesNotExist_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var parties = new Mock<IPartyDirectory>();
        parties.Setup(directory => directory.GetNameAsync(It.IsAny<PartyRef>(), It.IsAny<CancellationToken>())).ReturnsAsync((string)null);
        var arguments = Training();
        arguments["customer"] = "Account:404";

        // Act
        var answer = await _context.InvokeAsync(harness, new DraftEstimateTool(), arguments, services: new Dictionary<Type, object> { [typeof(IPartyDirectory)] = parties.Object });

        // Assert
        Assert.Contains("no customer is Account:404", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Draft_WithAnArgumentItDoesNotDeclare_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var arguments = Training();
        arguments["send"] = true;

        // Act
        var answer = await _context.InvokeAsync(harness, new DraftEstimateTool(), arguments);

        // Assert
        Assert.Contains("'send' is not an argument", answer, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> Training()
        => new()
        {
            ["customerName"] = "Contoso Ltd.",
            ["customerEmail"] = "ap@contoso.example",
            ["currency"] = "usd",
            ["depositPercent"] = 30m,
            ["onAcceptance"] = "full",
            ["lines"] = JsonSerializer.SerializeToElement(new[] { new { description = "Training", quantity = 3, unitPrice = 20 } }),
        };
}
