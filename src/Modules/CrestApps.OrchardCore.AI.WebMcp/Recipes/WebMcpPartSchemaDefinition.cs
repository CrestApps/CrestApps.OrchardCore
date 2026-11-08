using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.WebMcp.Recipes;

/// <summary>
/// Describes the <see cref="WebMcpPart"/> to CrestApps Recipes: its content type settings and the part data a content
/// item carries.
/// </summary>
public sealed class WebMcpPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => nameof(WebMcpPart);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The part has no settings of its own.")
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The AI tools the content item's page exposes through WebMCP, in addition to those the WebMCP settings give every page.")
            .Properties(
                (nameof(WebMcpPart.Tools), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("The names of the selectable tools and tool instances the content item's page exposes.")))
            .AdditionalProperties(true);
}
