using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.WebMcp.Recipes;

/// <summary>
/// Describes the <c>WebMcpSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class WebMcpSettingsSchema : SiteSettingsSchemaBase
{
    /// <inheritdoc />
    public override string Name => nameof(WebMcpSettings);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Which tools the site gives to browser agents through WebMCP, and on which pages.")
            .Properties(
                (nameof(WebMcpSettings.ExposeAllTools), Flag("Whether every selectable tool and tool instance is given to browser agents. When true, Tools is ignored.", false)),
                (nameof(WebMcpSettings.Tools), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("The names of the tools and tool instances given to browser agents when ExposeAllTools is false.")),
                (nameof(WebMcpSettings.EnableOnAdminPages), Flag("Whether admin pages offer WebMCP tools.", true)),
                (nameof(WebMcpSettings.EnableOnFrontEndPages), Flag("Whether front-end pages offer WebMCP tools.", false)),
                (nameof(WebMcpSettings.EnablePageTools), Flag("Whether the pages that get WebMCP also get the page tools, which read the page, list its links, open another page of the site, and read and fill its forms.", true)),
                (nameof(WebMcpSettings.EnableStorefrontTools), Flag("Whether front-end pages offer visitors who are not signed in, and signed-in users without the WebMCP permission, the selected tools that are marked safe for anonymous callers.", false)))
            .AdditionalProperties(true);

    private static JsonSchemaBuilder Flag(string description, bool defaultValue)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(defaultValue).Description(description);
}
