using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// An invoice keeps the date chosen on its draft, today when none was chosen. It can't be dated after today unless the
/// settings allow it, and, when the settings ask for chronological dates, not before the last invoice issued. A refused
/// date takes no number.
/// </summary>
public sealed class InvoiceIssueDateTests
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(InvoicingTestHarness.Now);

    [Fact]
    public async Task TheDateChosenOnTheDraft_IsKept_AndTheDueDateFollowsIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var chosen = _today.AddDays(-12);
        var draft = await harness.CreateDraftAsync(invoice => invoice.IssueDate = chosen);

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        var stored = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal((chosen, chosen), (stored.IssueDate, stored.DueDate));

        // Due at the end of the due date in the site's time zone (UTC in the harness).
        var transaction = await harness.FindTransactionAsync(stored.TransactionId);
        Assert.Equal(chosen, DateOnly.FromDateTime(transaction.DueUtc.Value));
    }

    [Fact]
    public async Task WithoutADate_TheInvoiceIsDatedToday()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();

        // Act
        await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(_today, (await harness.FindInvoiceAsync(draft.ItemId)).IssueDate);
    }

    [Fact]
    public async Task ADateAfterToday_IsRefused_AndTakesNoNumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync(invoice => invoice.IssueDate = _today.AddDays(7));

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.False(issued.Succeeded);
        Assert.Equal(0, harness.Numbers.Issued);

        var stored = await harness.FindInvoiceAsync(draft.ItemId);
        Assert.Equal((InvoiceStatus.Draft, (string)null), (stored.Status, stored.Number));
    }

    [Fact]
    public async Task ADateAfterToday_IsKept_WhenTheSettingsAllowIt()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { AllowFutureIssueDate = true });
        var chosen = _today.AddDays(7);
        var draft = await harness.CreateDraftAsync(invoice => invoice.IssueDate = chosen);

        // Act
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Assert
        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));
        Assert.Equal(chosen, (await harness.FindInvoiceAsync(draft.ItemId)).IssueDate);
    }

    [Fact]
    public async Task ADateBeforeTheLastInvoice_IsKept_WhenDatesNeedNotBeChronological()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await IssueDatedAsync(harness, _today.AddDays(-3));

        // Act
        var earlier = await IssueDatedAsync(harness, _today.AddDays(-8));

        // Assert
        Assert.True(earlier.Succeeded, string.Join(" ", earlier.Errors));
    }

    [Fact]
    public async Task ADateBeforeTheLastInvoice_IsRefused_WhenDatesMustBeChronological_AndTakesNoNumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync(new InvoicingSettings { ChronologicalIssueDates = true });
        var first = await IssueDatedAsync(harness, _today.AddDays(-3));
        var numbers = harness.Numbers.Issued;

        // Act
        var earlier = await IssueDatedAsync(harness, _today.AddDays(-8));
        var sameDay = await IssueDatedAsync(harness, _today.AddDays(-3));

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(earlier.Succeeded);
        Assert.Contains(first.Invoice.Number, earlier.Errors[0], StringComparison.Ordinal);
        Assert.True(sameDay.Succeeded, string.Join(" ", sameDay.Errors));
        Assert.Equal(numbers + 1, harness.Numbers.Issued);
    }

    private static async Task<CrestApps.OrchardCore.Invoicing.Services.InvoiceCommandResult> IssueDatedAsync(InvoicingTestHarness harness, DateOnly date)
    {
        var draft = await harness.CreateDraftAsync(invoice => invoice.IssueDate = date);

        return await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));
    }
}
