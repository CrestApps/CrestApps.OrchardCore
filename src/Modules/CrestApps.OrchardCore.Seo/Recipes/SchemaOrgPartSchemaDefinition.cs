using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Seo.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Seo.Recipes;

/// <summary>
/// Describes the <see cref="SchemaOrgPart"/> and its type settings to CrestApps Recipes.
/// </summary>
internal sealed class SchemaOrgPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => nameof(SchemaOrgPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(SchemaOrgPartSettings), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Description("The schema.org type of the content items and where each of their properties comes from.")
                    .Properties(
                        (nameof(SchemaOrgPartSettings.SchemaType), new JsonSchemaBuilder()
                            .Type(SchemaValueType.String | SchemaValueType.Null)
                            .Description("The schema.org type, such as BlogPosting, Article, Event or Product. When empty, a module may choose one (a product is a Product); other items are a WebPage.")),
                        (nameof(SchemaOrgPartSettings.Properties), new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    (nameof(SchemaOrgPropertyMapping.Name), new JsonSchemaBuilder()
                                        .Type(SchemaValueType.String)
                                        .Description("The schema.org property, such as headline. A dotted name, such as offers.price, sets a property of a nested object.")),
                                    (nameof(SchemaOrgPropertyMapping.Source), new JsonSchemaBuilder()
                                        .Type(SchemaValueType.String)
                                        .Enum(nameof(SchemaOrgValueSource.Content), nameof(SchemaOrgValueSource.Fixed), nameof(SchemaOrgValueSource.Editor), nameof(SchemaOrgValueSource.Liquid))
                                        .Description("Where the value comes from: Content (a field or value of the item), Fixed (the same value for every item), Editor (entered on each item) or Liquid (a template).")),
                                    (nameof(SchemaOrgPropertyMapping.Value), new JsonSchemaBuilder()
                                        .Type(SchemaValueType.String | SchemaValueType.Null)
                                        .Description("What the source needs: for Content the key of a field or value, such as Field:BlogPost.Image or Item:DisplayText; for Fixed the value itself; for Liquid the template; unused for Editor.")))
                                .Required(nameof(SchemaOrgPropertyMapping.Name))
                                .AdditionalProperties(true))
                            .Description("The properties and where their values come from, in the order they are written.")))
                    .AdditionalProperties(true)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(SchemaOrgPart.Disabled), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this content item's page publishes no structured data about it.")),
                (nameof(SchemaOrgPart.Values), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .AdditionalProperties(new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null))
                    .Description("The values entered on this item, keyed by property name, for the properties whose source is Editor.")))
            .AdditionalProperties(true);
}
