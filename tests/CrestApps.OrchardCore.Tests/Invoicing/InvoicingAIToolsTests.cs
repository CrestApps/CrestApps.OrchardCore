using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.AI;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// The invoicing AI tools act as the signed-in user: they find invoices, sum what is owed by currency and lateness,
/// draft (never issue) an invoice, and e-mail an open invoice again; without the permission they do nothing.
/// </summary>
public sealed class InvoicingAIToolsTests
{
    [Fact]
    public async Task FindAndSummarize_ListTheOverdueInvoice_AndAgeWhatIsOwed()
    {
        // Arrange: a $100 invoice issued October 1 (due then) and a $100 invoice issued today, both unpaid.
        await using var harness = await InvoicingTestHarness.CreateAsync();
        await IssueAsync(harness, new DateOnly(2026, 10, 1));
        await IssueAsync(harness, null);

        // Act
        var overdue = await InvokeAsync(harness, new FindInvoicesTool(), new() { ["status"] = "overdue" });
        var summary = await InvokeAsync(harness, new SummarizeReceivablesTool(), []);

        // Assert
        using var found = JsonDocument.Parse(overdue);
        Assert.Equal(1, found.RootElement.GetProperty("count").GetInt32());
        Assert.True(found.RootElement.GetProperty("invoices")[0].GetProperty("overdue").GetBoolean());

        using var sums = JsonDocument.Parse(summary);
        var usd = sums.RootElement.GetProperty("currencies")[0];
        Assert.Equal(("USD", 200m, 100m), (usd.GetProperty("currency").GetString(), usd.GetProperty("owed").GetDecimal(), usd.GetProperty("overdue").GetDecimal()));
        Assert.Equal(100m, usd.GetProperty("aging").GetProperty("days31To60").GetDecimal());
    }

    [Fact]
    public async Task DraftInvoice_MakesADraft_AndNothingIsDoneWithoutThePermission()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var arguments = new Dictionary<string, object>
        {
            ["customerName"] = "Contoso Ltd.",
            ["customerEmail"] = "ap@contoso.example",
            ["lines"] = JsonSerializer.SerializeToElement(new[] { new { description = "Training", quantity = 3, unitPrice = 20 } }),
        };

        // Act
        var refused = await InvokeAsync(harness, new DraftInvoiceTool(), arguments, allowed: false);
        var drafted = await InvokeAsync(harness, new DraftInvoiceTool(), arguments);

        // Assert
        Assert.Contains("not allowed", refused, StringComparison.Ordinal);
        using var result = JsonDocument.Parse(drafted);
        var invoice = await harness.FindInvoiceAsync(result.RootElement.GetProperty("id").GetString());
        Assert.Equal((InvoiceStatus.Draft, 60m, "Contoso Ltd."), (invoice.Status, invoice.Totals.Total, invoice.BillTo.Contact.Name));
        Assert.Equal(["Training"], invoice.Lines.Select(line => line.Name));
    }

    [Fact]
    public async Task SendInvoiceReminder_SendsAnOpenInvoice_ButNotOneThatOwesNothing()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var open = await IssueAsync(harness, null);
        var draft = await harness.CreateDraftAsync();
        var sender = new Mock<IInvoiceSender>();
        sender.Setup(item => item.SendAsync(It.IsAny<InvoiceSendRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InvoiceSendResult());

        // Act
        var sent = await InvokeAsync(harness, new SendInvoiceReminderTool(), new() { ["invoice"] = open.Number }, sender: sender.Object);
        var skipped = await InvokeAsync(harness, new SendInvoiceReminderTool(), new() { ["invoice"] = draft.ItemId }, sender: sender.Object);

        // Assert
        Assert.Contains("e-mailed again", sent, StringComparison.Ordinal);
        Assert.Contains("owes nothing", skipped, StringComparison.Ordinal);
        sender.Verify(item => item.SendAsync(It.Is<InvoiceSendRequest>(request => request.InvoiceId == open.ItemId), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<Invoice> IssueAsync(InvoicingTestHarness harness, DateOnly? issueDate)
    {
        var draft = await harness.CreateDraftAsync(invoice => invoice.IssueDate = issueDate);
        var issued = await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        Assert.True(issued.Succeeded, string.Join(" ", issued.Errors));

        return issued.Invoice;
    }

    private static Task<string> InvokeAsync(InvoicingTestHarness harness, AIFunction tool, Dictionary<string, object> values, bool allowed = true, IInvoiceSender sender = null)
        => harness.InScopeAsync(async services =>
        {
            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "pat")], "test")), RequestServices = services },
            };
            var overlay = new Overlay(services, new Dictionary<Type, object>
            {
                [typeof(IAuthorizationService)] = authorization.Object,
                [typeof(IHttpContextAccessor)] = accessor,
                [typeof(IInvoiceSender)] = sender,
            });

            var result = await tool.InvokeAsync(new AIFunctionArguments(values) { Services = overlay }, TestContext.Current.CancellationToken);

            return result?.ToString();
        });

    private sealed class Overlay(IServiceProvider inner, IReadOnlyDictionary<Type, object> overrides) : IServiceProvider
    {
        public object GetService(Type serviceType)
            => overrides.TryGetValue(serviceType, out var value) && value is not null ? value : inner.GetService(serviceType);
    }
}
