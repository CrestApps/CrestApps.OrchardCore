using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Recipes;
using CrestApps.OrchardCore.Taxation.Handlers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// A tax or shipping catalog entry's handler rejects a name another entry already uses, so the editor, a recipe and a
/// service that writes through the manager all keep names unique.
/// </summary>
public sealed class CatalogEntryNameUniquenessTests
{
    [Fact]
    public async Task ATaxCategory_WithTheNameOfAnotherCategory_IsRejected()
    {
        // Arrange
        var catalog = new InMemoryNamedCatalog<TaxCategory>();
        await catalog.CreateAsync(new TaxCategory { ItemId = "category-1", Name = "Food", Code = "food" }, TestContext.Current.CancellationToken);

        var handler = new TaxCategoryHandler(
            new HttpContextAccessor(),
            catalog,
            new TestClock(DateTime.UtcNow),
            new PassThroughStringLocalizer<TaxCategoryHandler>());

        var context = new ValidatingContext<TaxCategory>(new TaxCategory { ItemId = "category-2", Name = "food", Code = "groceries" });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ATaxCategory_KeepingItsOwnName_IsAccepted()
    {
        // Arrange
        var catalog = new InMemoryNamedCatalog<TaxCategory>();
        await catalog.CreateAsync(new TaxCategory { ItemId = "category-1", Name = "Food", Code = "food" }, TestContext.Current.CancellationToken);

        var handler = new TaxCategoryHandler(
            new HttpContextAccessor(),
            catalog,
            new TestClock(DateTime.UtcNow),
            new PassThroughStringLocalizer<TaxCategoryHandler>());

        var context = new ValidatingContext<TaxCategory>(new TaxCategory { ItemId = "category-1", Name = "Food", Code = "food-and-drink" });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task AShippingZone_WithTheNameOfAnotherZone_IsRejected()
    {
        // Arrange
        var catalog = new InMemoryNamedCatalog<ShippingZone>();
        await catalog.CreateAsync(new ShippingZone { ItemId = "zone-1", Name = "Domestic", Countries = ["US"] }, TestContext.Current.CancellationToken);

        var handler = new ShippingZoneHandler(
            new HttpContextAccessor(),
            catalog,
            new TestClock(DateTime.UtcNow),
            new PassThroughStringLocalizer<ShippingZoneHandler>());

        var context = new ValidatingContext<ShippingZone>(new ShippingZone { ItemId = "zone-2", Name = "Domestic", Countries = ["CA"] });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task TheShippingZonesStep_UpdatesTheZoneOfTheSameName_WhenTheRecipeGivesNoId()
    {
        // Arrange
        var stored = new ShippingZone { ItemId = "zone-1", Name = "Domestic", Countries = ["US"] };
        var manager = new Mock<INamedCatalogManager<ShippingZone>>();
        manager.Setup(value => value.FindByNameAsync("Domestic", It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        manager.Setup(value => value.ValidateAsync(It.IsAny<ShippingZone>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResultDetails());

        var step = new ShippingZonesStep(manager.Object);
        var context = new RecipeExecutionContext
        {
            Name = ShippingConstants.ZonesRecipeStep,
            Step = new JsonObject
            {
                ["name"] = ShippingConstants.ZonesRecipeStep,
                ["Zones"] = new JsonArray(new JsonObject { ["Name"] = " Domestic ", ["Countries"] = new JsonArray("us", "ca") }),
            },
        };

        // Act
        await ((IRecipeStepHandler)step).ExecuteAsync(context);

        // Assert
        Assert.Empty(context.Errors);
        manager.Verify(value => value.UpdateAsync(stored, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(value => value.CreateAsync(It.IsAny<ShippingZone>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
