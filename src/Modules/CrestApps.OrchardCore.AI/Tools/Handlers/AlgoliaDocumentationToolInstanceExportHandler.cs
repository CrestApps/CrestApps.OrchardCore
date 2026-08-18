using CrestApps.Core.AI.Tooling.Instances.Documentation;

namespace CrestApps.OrchardCore.AI.Tools.Handlers;

/// <summary>
/// Removes the Algolia search-only API key from the export payload of an Algolia documentation search tool
/// instance so that the stored credential is never written to a deployment plan or recipe.
/// </summary>
internal sealed class AlgoliaDocumentationToolInstanceExportHandler : IAIToolInstanceHandler
{
    /// <summary>
    /// Clears the Algolia API key from the export payload when the instance uses the Algolia documentation source.
    /// </summary>
    /// <param name="context">The context describing the tool instance being exported.</param>
    public void Exporting(ExportingAIToolInstanceContext context)
    {
        if (!string.Equals(context.Instance.Source, DocumentationToolConstants.AlgoliaSourceName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var settingsNode = context.ExportData["Properties"]?[nameof(AlgoliaDocumentationToolSettings)]?.AsObject();

        if (settingsNode is null || settingsNode.Count == 0)
        {
            return;
        }

        // Always clear the API key during export to prevent accidental exposure of sensitive data.
        settingsNode[nameof(AlgoliaDocumentationToolSettings.ApiKey)] = string.Empty;
    }
}
