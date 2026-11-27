using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Drivers;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.ViewModels;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Assets;

/// <summary>
/// The asset editor is built by display drivers: the built-in driver fills and reads the fields the form always had, the
/// asset's rules are reported under the driver's prefix, and a driver another module registers adds its own section.
/// </summary>
public sealed class AssetEditorDriverTests
{
    [Fact]
    public async Task Driver_FillsTheEditorFromTheAsset()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync();
        var type = await AddVehicleTypeAsync(harness);
        var asset = new Asset
        {
            ItemId = "asset-1",
            Name = "Van 3",
            Number = "A-00003",
            AssetTypeId = type.ItemId,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Seats"] = "3" },
            Documents = ["manuals/van.pdf", "certificates/mot.pdf"],
            Fleet = new AssetFleet { Plate = "AB12 CDE", Schedulable = true },
        };

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = ((IDisplayDriver<Asset>)CreateDriver(services, fleet: true)).BuildEditorAsync(asset, BuildContext(isNew: false));

            // One shape for each card: Details, Owner and Place, Installation and Warranty, Type Details, Documents, Vehicle
            // and Fleet.
            var models = await DisplayResultModels.BuildAsync<AssetEditViewModel>(await result);

            Assert.Equal(7, models.Count);

            // Every card fills the asset's values; the Details card also carries the type.
            Assert.All(models, card => Assert.Equal("asset-1", card.AssetId));

            return models[0];
        }, commit: false);

        // Assert
        Assert.Equal("asset-1", model.AssetId);
        Assert.Equal("Van 3", model.Name);
        Assert.Equal("3", model.AttributeValues["seats"]);
        Assert.Equal($"manuals/van.pdf{Environment.NewLine}certificates/mot.pdf", model.Documents);
        Assert.Equal("AB12 CDE", model.Plate);
        Assert.True(model.Schedulable);
        Assert.True(model.ShowFleet);
        Assert.True(model.ShowVehicle);
        Assert.Equal(type.ItemId, model.Type.ItemId);
        Assert.Equal([type.ItemId], model.TypeOptions.Where(option => option.Selected).Select(option => option.Value));
    }

    [Fact]
    public async Task Driver_AppliesThePostedFields_AndKeepsOnlyTheTypesAttributes()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync();
        var type = await AddVehicleTypeAsync(harness);
        var asset = new Asset { Location = new() { AddressLine1 = "1 Main St", City = "Springfield", Latitude = 1, Longitude = 2 } };
        var posted = new AssetEditViewModel
        {
            AssetTypeId = type.ItemId,
            Name = "  Van 4 ",
            Number = " ",
            OwnerKind = AssetOwnerKind.Contact,
            OwnerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [nameof(AssetOwnerKind.Contact)] = "c1" },
            AddressLine1 = "2 Main St",
            City = "Springfield",
            AttributeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Seats"] = " 5 ", ["Color"] = "Red" },
            Documents = "manuals/van.pdf\r\n\r\n  certificates/mot.pdf ",
            Plate = "XY99",
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        await harness.RunAsync(services => ((IDisplayDriver<Asset>)CreateDriver(services, fleet: true)).UpdateEditorAsync(asset, context), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(type.ItemId, asset.AssetTypeId);
        Assert.Equal("Van 4", asset.Name);
        Assert.Null(asset.Number);
        Assert.Equal(AssetOwnerKind.Contact, asset.OwnerKind);
        Assert.Equal("c1", asset.OwnerId);
        Assert.Equal("2 Main St", asset.Location.AddressLine1);
        Assert.Null(asset.Location.Latitude);
        Assert.Equal(["Seats"], asset.Attributes.Keys);
        Assert.Equal("5", asset.Attributes["Seats"]);
        Assert.Equal(["manuals/van.pdf", "certificates/mot.pdf"], asset.Documents);

        // A vehicle a contact owns is not part of the fleet.
        Assert.Null(asset.Fleet);
    }

    [Fact]
    public async Task Driver_KeepsAVehiclesPlateAndVin_WithoutFleet()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync();
        var type = await AddVehicleTypeAsync(harness);
        var asset = new Asset();
        var posted = new AssetEditViewModel
        {
            AssetTypeId = type.ItemId,
            Name = "Van 5",
            Plate = " 8ABC123 ",
            Vin = "1FTBW2CM5HKA12345",
            Schedulable = true,
            CarriesStock = true,
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var driver = (IDisplayDriver<Asset>)CreateDriver(services, fleet: false);

            await driver.UpdateEditorAsync(asset, context);

            return (await DisplayResultModels.BuildAsync<AssetEditViewModel>(await driver.BuildEditorAsync(asset, BuildContext(isNew: false))))[0];
        }, commit: false);

        // Assert: the plate and VIN are kept; booking and stock are Fleet's, which is off.
        Assert.Equal("8ABC123", asset.Fleet.Plate);
        Assert.Equal("1FTBW2CM5HKA12345", asset.Fleet.Vin);
        Assert.False(asset.Fleet.Schedulable);
        Assert.False(asset.Fleet.CarriesStock);
        Assert.True(model.ShowVehicle);
        Assert.False(model.ShowFleet);
    }

    [Fact]
    public void TheBuiltInDriver_IsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new global::CrestApps.OrchardCore.Assets.Startup().ConfigureServices(services);

        // Assert
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IDisplayDriver<Asset>) && descriptor.ImplementationType == typeof(AssetDisplayDriver));
    }

    [Fact]
    public async Task Editor_ShowsTheSectionOfADriverAnotherModuleRegisters_AfterTheBuiltInFields()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<Asset>>(provider => CreateDriver(provider, fleet: false))
            .AddDisplayDriver<Asset, ExtraSectionDisplayDriver<Asset>>());

        // Act
        var shapes = await harness.RunAsync(async services =>
        {
            var editor = await services.GetRequiredService<IDisplayManager<Asset>>().BuildEditorAsync(new Asset { Name = "Boiler" }, new PostedFormUpdateModel(null), isNew: true);

            return EditorDisplayManagement.ZoneShapes(editor);
        }, commit: false);

        // Assert
        // The editor is one shape for each card; the other module's section is placed at Content:5, so it sits by position
        // among the cards (after Documents, which holds the same position, and before Vehicle and Fleet).
        Assert.Equal(
            ["AssetFields_Edit", "AssetOwner_Edit", "AssetWarranty_Edit", "AssetAttributes_Edit", "AssetDocuments_Edit", ExtraSectionDisplayDriver<Asset>.ShapeType, "AssetVehicle_Edit", "AssetFleet_Edit"],
            shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("Boiler", Assert.IsAssignableFrom<AssetEditViewModel>(shapes[0]).Name);
        Assert.All(shapes.Where(shape => shape.Metadata.Type != ExtraSectionDisplayDriver<Asset>.ShapeType), shape => Assert.Equal(nameof(Asset), shape.Metadata.Prefix));
        Assert.Equal(ExtraSectionDisplayDriver<Asset>.Note, Assert.IsAssignableFrom<ExtraSectionViewModel>(shapes[5]).Note);
    }

    [Fact]
    public async Task Editor_ReportsTheAssetsRulesAndEverySectionsErrors_UnderTheirPrefix()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<Asset>>(provider => CreateDriver(provider, fleet: false))
            .AddDisplayDriver<Asset, RefusingSectionDisplayDriver<Asset>>());
        var updater = new PostedFormUpdateModel(new AssetEditViewModel { Name = " ", OwnerKind = AssetOwnerKind.Customer });

        // Act: what the controller does on save.
        var isValid = await harness.RunAsync(async services =>
        {
            var asset = await services.GetRequiredService<AssetManager>().NewAsync();

            await services.GetRequiredService<IDisplayManager<Asset>>().UpdateEditorAsync(asset, updater, isNew: true);

            return await CatalogEntryValidation.ValidateAsync(services.GetRequiredService<AssetManager>(), asset, updater, nameof(Asset));
        }, commit: false);

        // Assert
        Assert.False(isValid);
        Assert.Equal(["The name is required."], updater.ModelState["Asset.Name"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal(["Choose the asset's type."], updater.ModelState["Asset.AssetTypeId"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal(["Choose who owns the asset."], updater.ModelState["Asset.OwnerId"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal([RefusingSectionDisplayDriver<Asset>.Error], updater.ModelState["Asset.Note"].Errors.Select(error => error.ErrorMessage));
    }

    private static AssetDisplayDriver CreateDriver(IServiceProvider services, bool fleet)
        => new(
            services.GetRequiredService<AssetTypeManager>(),
            services.GetRequiredService<AssetOwnerDirectory>(),
            services.GetRequiredService<ISession>(),
            fleet ? [new FleetFeature()] : [],
            RecipeRoundTrip.Localizer<AssetDisplayDriver>());

    private static UpdateEditorContext BuildContext(bool isNew)
        => PostedFormUpdateModel.CreateContext(null, isNew);

    private static Task<AssetType> AddVehicleTypeAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetTypeManager>();
            var type = await manager.NewAsync();

            type.Name = "Van";
            type.Category = AssetCategory.Vehicle;
            type.Attributes = [new AssetAttributeDefinition { Name = "Seats", Kind = AssetAttributeKind.Number }];
            await manager.CreateAsync(type);

            return type;
        });
}
