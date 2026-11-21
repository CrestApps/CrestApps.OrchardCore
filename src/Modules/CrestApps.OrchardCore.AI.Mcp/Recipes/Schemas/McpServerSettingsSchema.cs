using CrestApps.OrchardCore.AI.Mcp.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes.Schemas;

/// <summary>
/// Describes the <c>McpServerSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class McpServerSettingsSchema : SiteSettingsSchemaBase
{
    /// <inheritdoc />
    public override string Name => nameof(McpServerSettings);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How MCP clients sign in to the site's MCP server and which tools it exposes. Values in the shell configuration override these.")
            .Properties(
                (nameof(McpServerSettings.AuthenticationType), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("OpenId", "ApiKey")
                    .Default("OpenId")
                    .Description("OpenId signs clients in with the site's OpenID server; ApiKey requires the API key below.")),
                (nameof(McpServerSettings.ApiKey), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The API key clients send when AuthenticationType is ApiKey. A key given in a recipe is stored as given until the settings are saved with a new key, which the settings editor stores encrypted, so a recipe should not carry a production key.")),
                (nameof(McpServerSettings.RequireAccessPermission), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(true)
                    .Description("Whether OpenId clients also need the AccessMcpServer permission.")),
                (nameof(McpServerSettings.ExposeAllTools), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(false)
                    .Description("Whether every non-hidden tool and tool instance is exposed. When true, Tools is ignored.")),
                (nameof(McpServerSettings.Tools), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("The names of the tools and tool instances exposed when ExposeAllTools is false.")))
            .AdditionalProperties(true);
}
