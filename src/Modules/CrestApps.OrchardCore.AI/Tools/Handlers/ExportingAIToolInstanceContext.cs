using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;

namespace CrestApps.OrchardCore.AI.Tools.Handlers;

/// <summary>
/// Represents the context provided to <see cref="IAIToolInstanceHandler"/> implementations while an AI tool
/// instance is being exported, allowing a handler to remove sensitive data from the export payload.
/// </summary>
public sealed class ExportingAIToolInstanceContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportingAIToolInstanceContext"/> class.
    /// </summary>
    /// <param name="instance">The tool instance being exported.</param>
    /// <param name="exportData">The JSON object that represents the exported tool instance.</param>
    public ExportingAIToolInstanceContext(
        AIToolInstance instance,
        JsonObject exportData)
    {
        Instance = instance;
        ExportData = exportData;
    }

    /// <summary>
    /// Gets the tool instance being exported.
    /// </summary>
    public AIToolInstance Instance { get; }

    /// <summary>
    /// Gets the JSON object that represents the exported tool instance.
    /// </summary>
    public JsonObject ExportData { get; }
}
