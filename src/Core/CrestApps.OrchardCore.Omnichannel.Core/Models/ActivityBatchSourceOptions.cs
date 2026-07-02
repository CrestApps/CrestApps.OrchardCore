using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Configures the available activity batch sources.
/// </summary>
public sealed class ActivityBatchSourceOptions
{
    private readonly Dictionary<string, ActivityBatchSourceEntry> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered activity batch sources.
    /// </summary>
    public IReadOnlyDictionary<string, ActivityBatchSourceEntry> Sources
        => _sources;

    /// <summary>
    /// Adds or updates an activity batch source.
    /// </summary>
    /// <param name="source">The unique source identifier.</param>
    /// <param name="configure">An optional configuration action.</param>
    public void AddSource(string source, Action<ActivityBatchSourceEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (!_sources.TryGetValue(source, out var entry))
        {
            entry = new ActivityBatchSourceEntry(source);
        }

        configure?.Invoke(entry);

        entry.DisplayName ??= new LocalizedString(source, source);
        entry.Description ??= new LocalizedString(source, source);

        _sources[source] = entry;
    }
}

/// <summary>
/// Represents a registered activity batch source entry.
/// </summary>
public sealed class ActivityBatchSourceEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityBatchSourceEntry"/> class.
    /// </summary>
    /// <param name="source">The unique source identifier.</param>
    public ActivityBatchSourceEntry(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        Source = source;
    }

    /// <summary>
    /// Gets the unique source identifier.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown in the source selection dialog.
    /// </summary>
    public LocalizedString Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether batches from this source require user assignment while loading.
    /// </summary>
    public bool RequiresUserAssignment { get; set; } = true;
}
