using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Recipes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The tax and shipping recipe steps check an update before they save it, so an invalid entry is reported as a recipe
/// error and the stored entry is left as it was.
/// </summary>
public sealed class CatalogRecipeStepValidationTests
{
    [Fact]
    public async Task TheTaxCategoryStep_ChecksAnUpdateBeforeSavingIt()
    {
        // Arrange
        var stored = new TaxCategory { ItemId = "category-1", Name = "Food", Code = "food" };
        var manager = new Mock<INamedCatalogManager<TaxCategory>>();
        manager.Setup(value => value.FindByIdAsync("category-1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        manager.Setup(value => value.ValidateAsync(It.IsAny<TaxCategory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxCategory category, CancellationToken _) => Validate(!string.IsNullOrWhiteSpace(category.Name)));

        var step = new TaxCategoryStep(manager.Object, new PassThroughStringLocalizer<TaxCategoryStep>());
        var context = Context(TaxationDeploymentSteps.TaxCategory, "TaxCategories", new JsonObject { ["ItemId"] = "category-1", ["Name"] = string.Empty });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);
        Assert.Equal("Food", stored.Name);
        manager.Verify(value => value.UpdateAsync(It.IsAny<TaxCategory>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheTaxCategoryStep_SavesAValidUpdate()
    {
        // Arrange
        var stored = new TaxCategory { ItemId = "category-1", Name = "Food", Code = "food" };
        var manager = new Mock<INamedCatalogManager<TaxCategory>>();
        manager.Setup(value => value.FindByIdAsync("category-1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        manager.Setup(value => value.ValidateAsync(It.IsAny<TaxCategory>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxCategory category, CancellationToken _) => Validate(!string.IsNullOrWhiteSpace(category.Name)));

        var step = new TaxCategoryStep(manager.Object, new PassThroughStringLocalizer<TaxCategoryStep>());
        var context = Context(TaxationDeploymentSteps.TaxCategory, "TaxCategories", new JsonObject { ["ItemId"] = "category-1", ["Name"] = "Groceries" });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(value => value.UpdateAsync(stored, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheShippingZonesStep_ChecksAnUpdateBeforeSavingIt()
    {
        // Arrange
        var stored = new ShippingZone { ItemId = "zone-1", Name = "Domestic", Countries = ["US"] };
        var manager = new Mock<INamedCatalogManager<ShippingZone>>();
        manager.Setup(value => value.FindByIdAsync("zone-1", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        manager.Setup(value => value.ValidateAsync(It.IsAny<ShippingZone>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShippingZone zone, CancellationToken _) => Validate(zone.RestOfWorld || zone.Countries.Count > 0));

        var step = new ShippingZonesStep(manager.Object);
        var context = Context(ShippingConstants.ZonesRecipeStep, "Zones", new JsonObject { ["ItemId"] = "zone-1", ["Countries"] = new JsonArray() });

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Single(context.Errors);
        Assert.Equal(["US"], stored.Countries);
        manager.Verify(value => value.UpdateAsync(It.IsAny<ShippingZone>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RecipeExecutionContext Context(string name, string member, JsonObject entry)
        => new()
        {
            Name = name,
            Step = new JsonObject
            {
                ["name"] = name,
                [member] = new JsonArray(entry),
            },
        };

    private static ValidationResultDetails Validate(bool isValid)
    {
        var result = new ValidationResultDetails();

        if (!isValid)
        {
            result.Fail(new ValidationResult("Not valid."));
        }

        return result;
    }
}
