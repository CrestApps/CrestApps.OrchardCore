using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Indexing;

/// <summary>
/// Stands in for the index manager of one search provider in a tool test: it answers each call with the test's
/// setting and records the calls, in order.
/// </summary>
internal sealed class FakeIndexManager : IIndexManager
{
    /// <summary>
    /// Gets or sets a value indicating whether the physical indexes exist.
    /// </summary>
    public bool Exists { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the provider creates an index.
    /// </summary>
    public bool Creates { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the provider recreates an index.
    /// </summary>
    public bool Rebuilds { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the provider removes an index.
    /// </summary>
    public bool Deletes { get; set; } = true;

    /// <summary>
    /// Gets the calls that changed a physical index, in order, for example <c>Rebuild:blog</c>.
    /// </summary>
    public List<string> Calls { get; } = [];

    public Task<bool> CreateAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Create:{indexProfile.Name}");

        return Task.FromResult(Creates);
    }

    public Task<bool> RebuildAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Rebuild:{indexProfile.Name}");

        return Task.FromResult(Rebuilds);
    }

    public Task<bool> DeleteAsync(IndexProfile indexProfile)
    {
        Calls.Add($"Delete:{indexProfile.Name}");

        return Task.FromResult(Deletes);
    }

    public Task<bool> ExistsAsync(string indexFullName)
        => Task.FromResult(Exists);
}
