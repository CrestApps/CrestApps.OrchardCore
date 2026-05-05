using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class CampaignActionOptions
{
    private readonly Dictionary<string, CampaignActionTypeEntry> _actionTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, CampaignActionTypeEntry> ActionTypes
        => _actionTypes;

    public void AddActionType(string type, Action<CampaignActionTypeEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        if (!_actionTypes.TryGetValue(type, out var entry))
        {
            entry = new CampaignActionTypeEntry(type);
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

public sealed class CampaignActionTypeEntry
{
    public CampaignActionTypeEntry(string type)
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
