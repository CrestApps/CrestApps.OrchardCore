using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// A transaction posted for an invoice says which invoice, by its number, instead of a raw reference.
/// </summary>
public sealed class InvoiceCheckoutReferenceDescriberTests
{
    [Fact]
    public async Task AnInvoiceReference_IsNamedByTheInvoiceNumber()
    {
        // Arrange
        await using var harness = await InvoicingTestHarness.CreateAsync();
        var draft = await harness.CreateDraftAsync();
        await harness.RunAsync(commands => commands.IssueAsync(draft.ItemId, TestContext.Current.CancellationToken));

        // Act
        var (issued, other) = await harness.InScopeAsync(async services =>
        {
            var describer = new InvoiceCheckoutReferenceDescriber(
                services.GetRequiredService<IInvoiceStore>(),
                Mock.Of<LinkGenerator>(),
                new HttpContextAccessor(),
                new PassThroughStringLocalizer<InvoiceCheckoutReferenceDescriber>());

            return (
                await describer.DescribeAsync(InvoicingConstants.InvoiceReferenceType, draft.ItemId, TestContext.Current.CancellationToken),
                await describer.DescribeAsync("Order", draft.ItemId, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal("Invoice INV-1001", issued.Text);
        Assert.Null(other);
    }
}
