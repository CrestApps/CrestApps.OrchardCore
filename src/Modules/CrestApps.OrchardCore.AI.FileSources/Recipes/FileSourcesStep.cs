using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.FileSources.Recipes;

/// <summary>
/// Adds or updates file sources from a <c>FileSources</c> recipe step. A file source is matched by its <c>ItemId</c>,
/// then by its name within the same connector, and its connector must be enabled on the site.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "FileSources", "Sources": [ { "ItemId": "...", "Source": "FileSystem", "DisplayText": "Policies", "AIDataSourceId": "...",
///   "Enabled": true, "ReindexIntervalMinutes": 60, "Properties": { ... } } ] }
/// </code>
/// Connection passwords and keys are never exported. A recipe that leaves them out keeps the stored ones; enter them on
/// the file source's page after importing a new one.
/// </remarks>
internal sealed class FileSourcesStep : IngestionSourceRecipeStep<FileSource>
{
    public const string StepKey = "FileSources";

    public const string EntriesName = "Sources";

    private readonly IngestionConnectorOptions _connectorOptions;

    internal readonly IStringLocalizer S;

    public FileSourcesStep(
        ISourceCatalogManager<FileSource> manager,
        IOptions<IngestionConnectorOptions> connectorOptions,
        IStringLocalizer<FileSourcesStep> stringLocalizer)
        : base(StepKey, EntriesName, manager)
    {
        _connectorOptions = connectorOptions.Value;
        S = stringLocalizer;
    }

    protected override bool IsKnownSource(string source)
        => _connectorOptions.Connectors.Any(connector => string.Equals(connector.Name, source, StringComparison.OrdinalIgnoreCase));

    protected override string UnknownSourceError(string displayText, string source)
        => string.IsNullOrEmpty(source)
            ? S["The file source '{0}' does not name its connector in Source.", displayText]
            : S["The file source '{0}' uses the connector '{1}', which is not enabled on this site.", displayText, source];
}
