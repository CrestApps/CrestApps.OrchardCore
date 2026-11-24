using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Invoicing.AI;

/// <summary>
/// <c>convertEstimateToInvoice</c> turns a sent estimate into a draft invoice, as the estimate's Invoice buttons and Invoice
/// part form do: it needs both the estimates and the invoices permissions, returns the same invoice when asked again, and
/// bills a percent of the lines for progress invoicing.
/// </summary>
public sealed class ConvertEstimateToInvoiceToolTests
{
    private readonly InvoicingCreditToolContext _context = new();

    [Fact]
    public async Task Convert_IsRefused_WithoutTheInvoicesPermission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new ConvertEstimateToInvoiceTool(), new() { ["estimate"] = estimate.Number, ["conversion"] = "full" }, InvoicingCreditToolContext.AllBut(InvoicingPermissions.ManageInvoices));

        // Assert
        Assert.Contains("not allowed", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Convert_MakesADraftInvoice_AndReturnsItAgainTheSecondTime()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);

        // Act
        var first = await _context.InvokeAsync(harness, new ConvertEstimateToInvoiceTool(), new() { ["estimate"] = estimate.Number, ["conversion"] = "full" });
        var second = await _context.InvokeAsync(harness, new ConvertEstimateToInvoiceTool(), new() { ["estimate"] = estimate.Number, ["conversion"] = "full" });

        // Assert
        using var made = JsonDocument.Parse(first);
        using var again = JsonDocument.Parse(second);
        var invoice = made.RootElement.GetProperty("invoice");
        Assert.Equal(("draft", 100m), (invoice.GetProperty("status").GetString(), invoice.GetProperty("total").GetDecimal()));
        Assert.True(again.RootElement.GetProperty("alreadyDone").GetBoolean());
        Assert.Equal(invoice.GetProperty("id").GetString(), again.RootElement.GetProperty("invoice").GetProperty("id").GetString());

        var stored = await harness.FindInvoiceAsync(invoice.GetProperty("id").GetString());
        Assert.Equal(InvoiceStatus.Draft, stored.Status);
    }

    [Fact]
    public async Task Convert_InProgress_BillsThePercentOfEachLine()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new ConvertEstimateToInvoiceTool(), new() { ["estimate"] = estimate.Number, ["conversion"] = "progress", ["percent"] = 50m });

        // Assert
        using var json = JsonDocument.Parse(answer);
        Assert.Equal(50m, json.RootElement.GetProperty("invoice").GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Convert_InProgress_WithoutAPercent_IsRefused()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var estimate = await SendAsync(harness);

        // Act
        var answer = await _context.InvokeAsync(harness, new ConvertEstimateToInvoiceTool(), new() { ["estimate"] = estimate.Number, ["conversion"] = "progress" });

        // Assert
        Assert.Contains("give either percent", answer, StringComparison.Ordinal);
    }

    private static async Task<Estimate> SendAsync(InvoicingTestHarness harness)
    {
        var draft = await InvoicingCreditToolContext.CreateEstimateAsync(harness);
        var sent = await harness.InScopeAsync(services => services.GetRequiredService<IEstimateCommandService>().SendAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(sent.Succeeded, string.Join(" ", sent.Errors));

        return sent.Estimate;
    }
}
