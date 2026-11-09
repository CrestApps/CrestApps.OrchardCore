using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using static CrestApps.OrchardCore.Assets.Recipes.AssetsSchemaBuilder;

namespace CrestApps.OrchardCore.Assets.Recipes;

/// <summary>
/// Describes the Assets recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AssetsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => AssetsRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= Step(
            AssetsRecipeStep.RecipeName,
            "Creates or updates asset types, then assets. Records are matched by ItemId only; an existing record takes only the members given. List parent assets before their children.",
            ("Types", Array(TypeEntry(), "The asset types.")),
            ("Assets", Array(AssetEntry(), "The assets, parents first. Their meter readings are never imported.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder TypeEntry()
    {
        var attribute = Object(
            "An attribute every asset of the type has.",
            ("Name", Text("The attribute name.")),
            ("Kind", Enum<AssetAttributeKind>("The kind of value.")));

        var meter = Object(
            "A meter assets of the type are read on, such as hours or miles.",
            ("Name", Text("The meter name.")),
            ("Unit", Text("The unit of a reading.")),
            ("Cumulative", Flag("Whether readings only go up, like an odometer. Defaults to true.")));

        return Object(
            "An asset type.",
            ("ItemId", ItemId("asset type")),
            ("Name", Text("The type name.")),
            ("Category", Enum<AssetCategory>("What kind of asset it is.")),
            ("Description", Text("What the type is.")),
            ("Attributes", Array(attribute, "The attributes its assets have.")),
            ("Meters", Array(meter, "The meters its assets are read on.")));
    }

    private static JsonSchemaBuilder AssetEntry()
        => Object(
            "An asset: a piece of equipment, vehicle, tool, customer unit, component or facility.",
            ("ItemId", ItemId("asset")),
            ("Number", Text("The asset number.")),
            ("Name", Text("The asset name.")),
            ("AssetTypeId", Text("The ItemId of its asset type.")),
            ("ProductId", Text("The content item id of the product it is.")),
            ("SerialNumber", Text("The serial number.")),
            ("Manufacturer", Text("The manufacturer.")),
            ("Model", Text("The model.")),
            ("OwnerKind", Enum<AssetOwnerKind>("Who owns it. The owner itself (OwnerId) is not carried by this step: deployment plans leave it out and an import ignores it.")),
            ("Location", Address("Where it is.")),
            ("ParentAssetId", Text("The ItemId of the asset it is part of.")),
            ("Status", Enum<AssetStatus>("Where the asset is in its life.")),
            ("InstallDate", OptionalDate("When it was installed.")),
            ("Warranty", OptionalObject(
                "Its warranty.",
                ("StartDate", OptionalDate("When the warranty starts.")),
                ("EndDate", OptionalDate("When the warranty ends.")),
                ("Provider", Text("Who gives the warranty.")),
                ("Terms", Text("Its terms.")))),
            ("Attributes", new JsonSchemaBuilder().Type(SchemaValueType.Object).AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null)).Description("The values of its type's attributes, by attribute name.")),
            ("Documents", Strings("The paths of its documents in the media library.")),
            ("Fleet", OptionalObject(
                "Its fleet data, for vehicles, equipment and tools the business owns.",
                ("Schedulable", Flag("Whether it is booked for visits, with its own Equipment calendar that refuses double-booking.")),
                ("CalendarId", Text("The id of its Equipment calendar.")),
                ("Plate", Text("The registration plate.")),
                ("Vin", Text("The vehicle identification number.")),
                ("Capacity", Text("What it can carry or hold.")))));
}
