using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The TaxationPart editor: its settings hide the classification and external code, and an editor that was not on the
/// form leaves the part as it is.
/// </summary>
public sealed class TaxationPartDisplayDriverTests
{
    [Fact]
    public async Task UpdateAsync_WithTheClassificationAndExternalCodeHidden_KeepsThem()
    {
        // Arrange: the type shows Taxable and Tax category alone; a doctored post sends the hidden fields anyway.
        var driver = CreateDriver();
        var part = new TaxationPart { Taxable = false, TaxClassificationCode = "TELEVISION", ExternalTaxCode = "P0000000" };
        var posted = new TaxationPartViewModel { Rendered = true, Taxable = true, TaxCategoryCode = "GOODS", TaxClassificationCode = "OTHER", ExternalTaxCode = "X" };

        // Act
        await driver.UpdateAsync(part, Context(posted, new TaxationPartSettings { ShowClassification = false, ShowExternalTaxCode = false }));

        // Assert
        Assert.True(part.Taxable);
        Assert.Equal(("GOODS", "TELEVISION", "P0000000"), (part.TaxCategoryCode, part.TaxClassificationCode, part.ExternalTaxCode));
    }

    [Fact]
    public async Task UpdateAsync_WithEveryFieldShown_TakesThemAll()
    {
        // Arrange
        var driver = CreateDriver();
        var part = new TaxationPart();
        var posted = new TaxationPartViewModel { Rendered = true, Taxable = true, TaxCategoryCode = "GOODS", TaxClassificationCode = "TELEVISION", ExternalTaxCode = "P0000000" };

        // Act
        await driver.UpdateAsync(part, Context(posted, new TaxationPartSettings()));

        // Assert
        Assert.Equal(("GOODS", "TELEVISION", "P0000000"), (part.TaxCategoryCode, part.TaxClassificationCode, part.ExternalTaxCode));
    }

    [Fact]
    public async Task UpdateAsync_WhenTheEditorWasNotOnTheForm_LeavesThePartAsItIs()
    {
        // Arrange: the type placed the editor nowhere, so nothing of it is posted.
        var driver = CreateDriver();
        var part = new TaxationPart { Taxable = true, TaxCategoryCode = "GOODS", ExternalTaxCode = "P0000000" };

        // Act
        await driver.UpdateAsync(part, Context(new TaxationPartViewModel(), new TaxationPartSettings()));

        // Assert
        Assert.True(part.Taxable);
        Assert.Equal(("GOODS", "P0000000"), (part.TaxCategoryCode, part.ExternalTaxCode));
    }

    private static TaxationPartDisplayDriver CreateDriver()
        => new(new CrestApps.OrchardCore.Tests.Taxation.Fakes.InMemoryNamedCatalog<TaxCategory>(), new PassThroughStringLocalizer<TaxationPartDisplayDriver>());

    private static UpdatePartEditorContext Context(TaxationPartViewModel posted, TaxationPartSettings settings)
    {
        var definition = new ContentTypePartDefinition(
            nameof(TaxationPart),
            new ContentPartDefinition(nameof(TaxationPart)),
            new JsonObject { [nameof(TaxationPartSettings)] = JsonSerializer.SerializeToNode(settings) });

        return new UpdatePartEditorContext(definition, PostedFormUpdateModel.CreateContext(posted));
    }
}
