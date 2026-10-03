using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Lists the activity sources that can occur on activities, so the source filters and the bulk
/// <c>Change source</c> action only offer what the enabled features actually produce. Each feature that writes a
/// source onto activities registers it here, the same way it registers its activity batch source.
/// </summary>
public sealed class ActivitySourceOptions
{
    private readonly Dictionary<string, ActivitySourceEntry> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered activity sources, keyed by the value a filter selects.
    /// </summary>
    public IReadOnlyDictionary<string, ActivitySourceEntry> Sources
        => _sources;

    /// <summary>
    /// Adds an activity source, or updates the one already registered under the same value.
    /// </summary>
    /// <param name="source">The value a filter selects. It is also one of the stored values the source matches.</param>
    /// <param name="configure">An optional configuration action.</param>
    public void AddSource(string source, Action<ActivitySourceEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        if (!_sources.TryGetValue(source, out var entry))
        {
            entry = new ActivitySourceEntry(source);
        }

        configure?.Invoke(entry);

        entry.DisplayName ??= new LocalizedString(source, source);

        _sources[source] = entry;
    }

    /// <summary>
    /// Gets the stored <see cref="OmnichannelActivity.Source"/> values a selected filter value matches. A registered
    /// source matches every value it lists; any other value, such as an old link to a source that is no longer
    /// offered, matches only itself, so a filter never silently widens to everything.
    /// </summary>
    /// <param name="source">The selected filter value.</param>
    /// <returns>The stored values to match, or an empty array when no source is selected.</returns>
    public string[] GetStoredValues(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        var trimmed = source.Trim();

        return _sources.TryGetValue(trimmed, out var entry)
            ? [.. entry.StoredValues]
            : [trimmed];
    }

    /// <summary>
    /// Finds a registered source that may be set on activities by hand.
    /// </summary>
    /// <param name="source">The requested source value.</param>
    /// <param name="entry">The matching registered source, when one is found.</param>
    /// <returns><see langword="true"/> when the source is registered and may be set by hand.</returns>
    public bool TryGetManuallyAssignableSource(string source, out ActivitySourceEntry entry)
    {
        entry = null;

        if (string.IsNullOrWhiteSpace(source) ||
            !_sources.TryGetValue(source.Trim(), out var registered) ||
            !registered.CanBeSetManually)
        {
            return false;
        }

        entry = registered;

        return true;
    }
}

/// <summary>
/// Represents one activity source offered by the source filters.
/// </summary>
public sealed class ActivitySourceEntry
{
    private readonly List<string> _storedValues = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitySourceEntry"/> class.
    /// </summary>
    /// <param name="source">The value a filter selects.</param>
    public ActivitySourceEntry(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);

        Source = source;
        _storedValues.Add(source);
    }

    /// <summary>
    /// Gets the value a filter selects.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the bulk <c>Change source</c> action may set this source by hand.
    /// Sources that a feature owns, such as the dialer modes, are left to that feature.
    /// </summary>
    public bool CanBeSetManually { get; set; }

    /// <summary>
    /// Gets the stored <see cref="OmnichannelActivity.Source"/> values this source matches, starting with
    /// <see cref="Source"/> itself.
    /// </summary>
    public IReadOnlyList<string> StoredValues
        => _storedValues;

    /// <summary>
    /// Adds stored values this source matches, for example the per-mode values a dialer writes onto its activities.
    /// </summary>
    /// <param name="storedValues">The stored values to add.</param>
    /// <returns>This entry, so calls can be chained.</returns>
    public ActivitySourceEntry Matches(params string[] storedValues)
    {
        ArgumentNullException.ThrowIfNull(storedValues);

        foreach (var storedValue in storedValues)
        {
            if (!string.IsNullOrWhiteSpace(storedValue) &&
                !_storedValues.Contains(storedValue, StringComparer.OrdinalIgnoreCase))
            {
                _storedValues.Add(storedValue);
            }
        }

        return this;
    }
}
