using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Exports the entries of a catalog as one recipe step that holds the entries in one array.
/// </summary>
/// <remarks>
/// The step is named after the deployment step. A derived source says where the entries come from, which of them the
/// deployment step selected and how each one is written; by default an entry is written by
/// <see cref="CatalogDeploymentSerializer"/>, which leaves out the members the environment owns. A source whose recipe
/// step already reads a hand-written shape overrides <see cref="Export(TEntry)"/> to keep that shape.
/// </remarks>
/// <typeparam name="TStep">The deployment step type.</typeparam>
/// <typeparam name="TEntry">The catalog entry type.</typeparam>
public abstract class CatalogDeploymentSourceBase<TStep, TEntry> : DeploymentSourceBase<TStep>
    where TStep : DeploymentStep
    where TEntry : CatalogItem
{
    private readonly string _entriesName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogDeploymentSourceBase{TStep, TEntry}"/> class.
    /// </summary>
    /// <param name="entriesName">The name of the step member that holds the array of entries.</param>
    protected CatalogDeploymentSourceBase(string entriesName)
    {
        ArgumentException.ThrowIfNullOrEmpty(entriesName);

        _entriesName = entriesName;
    }

    /// <summary>
    /// Reads the stored entries, in the order they are written to the plan.
    /// </summary>
    /// <returns>The stored entries.</returns>
    protected abstract Task<IEnumerable<TEntry>> GetEntriesAsync();

    /// <summary>
    /// Says whether the deployment step selected an entry. Every entry is exported by default.
    /// </summary>
    /// <param name="step">The deployment step being processed.</param>
    /// <param name="entry">The stored entry.</param>
    /// <returns><see langword="true"/> when the entry is exported.</returns>
    protected virtual bool IsSelected(TStep step, TEntry entry)
        => true;

    /// <summary>
    /// Writes one entry into the JSON shape the matching recipe step reads.
    /// </summary>
    /// <param name="entry">The entry to export.</param>
    /// <returns>The JSON of the entry.</returns>
    protected virtual JsonObject Export(TEntry entry)
        => CatalogDeploymentSerializer.Export(entry);

    /// <inheritdoc/>
    protected override async Task ProcessAsync(TStep step, DeploymentPlanResult result)
    {
        var data = new JsonArray();

        foreach (var entry in await GetEntriesAsync())
        {
            if (!IsSelected(step, entry))
            {
                continue;
            }

            data.Add(Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [_entriesName] = data,
        });
    }

    /// <summary>
    /// Says whether an entry is selected by a deployment step that either includes every entry or lists the keys of
    /// the entries it includes. An empty list selects every entry, as an unfiltered step does.
    /// </summary>
    /// <param name="includeAll">Whether the step includes every entry.</param>
    /// <param name="selectedKeys">The keys the step lists, compared ordinally.</param>
    /// <param name="key">The key of the entry, its identifier or its name.</param>
    /// <returns><see langword="true"/> when the entry is selected.</returns>
    protected static bool IsSelectedByKey(bool includeAll, string[] selectedKeys, string key)
        => includeAll || selectedKeys is not { Length: > 0 } || selectedKeys.Contains(key, StringComparer.Ordinal);
}
