using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Invoicing;

/// <summary>
/// With Taxation enabled, invoice lines are taxed by its rules for the bill-to's party tax profile; without it Invoicing
/// registers nothing of Taxation's and uses the taxes typed on the lines.
/// </summary>
public sealed class TaxationDocumentTaxProviderTests
{
    [Fact]
    public async Task ApplyTaxes_SetsEachLinesTaxesFromTaxation_ForTheBillTosProfile()
    {
        // Arrange
        var party = new PartyRef(PartyKinds.Account, "account-1");
        var profiles = new Mock<IPartyTaxProfileService>();
        profiles.Setup(service => service.GetAsync(party, It.IsAny<CancellationToken>())).ReturnsAsync(new CustomerTaxProfile());

        TaxCalculationContext asked = null;
        var taxes = new Mock<ITaxService>();
        taxes
            .Setup(service => service.CalculateAsync(It.IsAny<TaxCalculationContext>(), It.IsAny<CancellationToken>()))
            .Callback((TaxCalculationContext context, CancellationToken _) => asked = context)
            .ReturnsAsync(new TaxCalculationResult
            {
                Lines = [new TaxLine { ItemId = "line-1", TaxName = "VAT", TaxCode = "S", Rate = 0.2m, TaxAmount = 20m }],
            });

        var context = new DocumentCalculationContext
        {
            Currency = "EUR",
            BillTo = new DocumentParty { Party = party },
            Lines =
            [
                new DocumentLine { Id = "line-1", Kind = DocumentLineKind.Service, Quantity = 1m, UnitPrice = 100m, DiscountPercent = 10m },
                new DocumentLine { Id = "note", Kind = DocumentLineKind.Text, Name = "Thanks" },
            ],
        };

        // Act
        var handled = await new TaxationDocumentTaxProvider(taxes.Object, profiles.Object, Mock.Of<global::OrchardCore.Modules.IClock>()).ApplyTaxesAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        var item = Assert.Single(asked.Items);
        Assert.Equal(("line-1", 10m, TaxableItemKind.Service), (item.Id, ((TaxableItem)item).DiscountAmount, ((TaxableItem)item).Kind));

        var tax = Assert.Single(context.Lines[0].Taxes);
        Assert.Equal(("VAT", 20m), (tax.Name, tax.Rate));
        profiles.VerifyAll();
    }

    [Fact]
    public void Invoicing_DependsOnTaxation_AndAlwaysRegistersItsTaxProvider()
    {
        // Arrange: everything about tax is the Taxation module's.
        var services = new ServiceCollection();
        var feature = typeof(CrestApps.OrchardCore.Invoicing.Startup).Assembly
            .GetCustomAttributes(typeof(global::OrchardCore.Modules.Manifest.FeatureAttribute), inherit: false)
            .Cast<global::OrchardCore.Modules.Manifest.FeatureAttribute>()
            .Single(attribute => attribute.GetType() == typeof(global::OrchardCore.Modules.Manifest.FeatureAttribute) && attribute.Id == CrestApps.OrchardCore.Invoicing.InvoicingConstants.Features.Area);

        // Act
        new CrestApps.OrchardCore.Invoicing.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(CrestApps.OrchardCore.Taxation.TaxationConstants.Feature.Taxation, feature.Dependencies);
        Assert.Contains(services, descriptor => descriptor.ImplementationType == typeof(TaxationDocumentTaxProvider));
    }
}
