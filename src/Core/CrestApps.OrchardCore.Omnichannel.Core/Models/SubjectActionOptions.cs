using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Configures the available subject action types.
/// </summary>
public sealed class SubjectActionOptions
{
    private readonly Dictionary<string, SubjectActionTypeEntry> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered action types.
    /// </summary>
    public IReadOnlyDictionary<string, SubjectActionTypeEntry> ActionTypes
        => _actionTypes;

    /// <summary>
    /// Adds or updates a subject action type.
    /// </summary>
    /// <param name="type">The unique type identifier.</param>
    /// <param name="configure">An optional configuration action.</param>
    public void AddActionType(string type, Action<SubjectActionTypeEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        if (!_actionTypes.TryGetValue(type, out var entry))
        {
            entry = new SubjectActionTypeEntry(type);
        }

        if (configure != null)
        {
            configure(entry);
        }

        if (string.IsNullOrEmpty(entry.DisplayName))
        {
            entry.DisplayName = new LocalizedString(type, type);
        }

        _actionTypes[type] = entry;
    }
}

/// <summary>
/// Represents a registered subject action type entry.
/// </summary>
public sealed class SubjectActionTypeEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubjectActionTypeEntry"/> class.
    /// </summary>
    /// <param name="type">The unique type identifier.</param>
    public SubjectActionTypeEntry(string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        Type = type;
    }

    /// <summary>
    /// Gets the unique type identifier for this action type.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the description shown in the type selection dialog.
    /// </summary>
    public LocalizedString Description { get; set; }
}
