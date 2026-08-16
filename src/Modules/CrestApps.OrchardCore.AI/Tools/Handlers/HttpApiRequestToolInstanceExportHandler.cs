using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling.Instances;

namespace CrestApps.OrchardCore.AI.Tools.Handlers;

/// <summary>
/// Removes the credentials stored by an HTTP API request tool instance from the export payload so that secrets
/// such as API keys, bearer tokens, passwords, client secrets, and cached OAuth tokens are never written to a
/// deployment plan or recipe.
/// </summary>
internal sealed class HttpApiRequestToolInstanceExportHandler : IAIToolInstanceHandler
{
    /// <summary>
    /// Clears the stored credentials from the export payload when the instance uses the HTTP API request source.
    /// </summary>
    /// <param name="context">The context describing the tool instance being exported.</param>
    public void Exporting(ExportingAIToolInstanceContext context)
    {
        if (!string.Equals(context.Instance.Source, HttpApiRequestToolConstants.SourceName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var propertiesNode = context.ExportData["Properties"]?.AsObject();

        if (propertiesNode is null)
        {
            return;
        }

        var settingsNode = propertiesNode[nameof(HttpApiRequestToolSettings)]?.AsObject();

        if (settingsNode is not null)
        {
            Clear(settingsNode, nameof(HttpApiRequestToolSettings.ApiKey));
            Clear(settingsNode, nameof(HttpApiRequestToolSettings.BearerToken));
            Clear(settingsNode, nameof(HttpApiRequestToolSettings.Password));
            Clear(settingsNode, nameof(HttpApiRequestToolSettings.ClientSecret));
        }

        var tokenStateNode = propertiesNode[nameof(HttpApiRequestTokenState)]?.AsObject();

        if (tokenStateNode is not null)
        {
            Clear(tokenStateNode, nameof(HttpApiRequestTokenState.AccessToken));
            Clear(tokenStateNode, nameof(HttpApiRequestTokenState.RefreshToken));
        }
    }

    private static void Clear(JsonObject node, string propertyName)
    {
        if (node.ContainsKey(propertyName))
        {
            node[propertyName] = string.Empty;
        }
    }
}
