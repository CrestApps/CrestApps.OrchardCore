using CrestApps.OrchardCore.Products.Feeds;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Products.Recipes;

/// <summary>
/// Describes the <c>ProductFeedSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ProductFeedSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(ProductFeedSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Settings › Commerce › Product feeds.")
            .Properties(
                ("DefaultBrand", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The brand of products that name none, such as the store's own.")),
                ("DefaultCondition", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("new").Description("The condition of products that name none: new, refurbished or used.")),
                ("DefaultGoogleProductCategory", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The Google product category of products that name none, as its id or full path.")),
                ("IncludeOutOfStock", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether products that are out of stock stay in the feed, marked so.")),
                ("Key", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("A key the feed's address has to carry (?key=...), or none for a public feed.")))
            .AdditionalProperties(true);
}
