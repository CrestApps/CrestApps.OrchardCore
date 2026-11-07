using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Seo.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Seo.Recipes;

/// <summary>
/// Describes the <c>StructuredDataSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StructuredDataSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(StructuredDataSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site's Schema.org structured data.")
            .Properties(
                (nameof(StructuredDataSettings.RenderOnPages), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the structured data is rendered on the site's pages.")),
                (nameof(StructuredDataSettings.PublisherType), new JsonSchemaBuilder().Type(SchemaValueType.String).Enum(StructuredDataPublisherTypes.Organization, StructuredDataPublisherTypes.Person).Description("Whether the site is published by an organization or a person.")),
                (nameof(StructuredDataSettings.PublisherName), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The publisher's name. The site name is used when it is empty.")),
                (nameof(StructuredDataSettings.PublisherLogoUrl), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The URL of the publisher's logo.")),
                (nameof(StructuredDataSettings.SameAs), new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The URLs of the publisher's profiles on other sites.")),
                (nameof(StructuredDataSettings.IncludeWebSite), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether a WebSite entry is rendered on the home page.")),
                (nameof(StructuredDataSettings.IncludeSearchAction), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the WebSite entry carries a search action.")))
            .AdditionalProperties(true);
}
