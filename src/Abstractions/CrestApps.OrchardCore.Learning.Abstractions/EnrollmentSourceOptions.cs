using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// The registered enrollment sources and their names, so lists and reports can show where a grant came from.
/// </summary>
public sealed class EnrollmentSourceOptions
{
    private readonly Dictionary<string, LocalizationSource> _sources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered sources with their display names.
    /// </summary>
    public IReadOnlyDictionary<string, LocalizationSource> Sources => _sources;

    /// <summary>
    /// Registers a source.
    /// </summary>
    /// <param name="name">The stable name of the source.</param>
    /// <param name="displayName">The name shown to people.</param>
    public void Add(string name, LocalizationSource displayName)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        _sources[name] = displayName;
    }
}
