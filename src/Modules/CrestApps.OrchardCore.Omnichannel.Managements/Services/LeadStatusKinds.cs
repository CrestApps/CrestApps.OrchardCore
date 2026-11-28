using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Turns the kind of a lead status and its default and contacted choices into its flags, for the lead status editor
/// and the AI tool that share the rule.
/// </summary>
internal static class LeadStatusKinds
{
    /// <summary>
    /// Gets the kind a lead status is.
    /// </summary>
    public static LeadStatusKind GetKind(LeadStatus status)
        => status.IsConverted
            ? LeadStatusKind.Converted
            : status.IsClosed ? LeadStatusKind.Closed : LeadStatusKind.Open;

    /// <summary>
    /// Sets the flags of a lead status from its kind and the default and contacted choices.
    /// </summary>
    public static void Apply(LeadStatus status, LeadStatusKind kind, bool isDefault, bool isContacted)
    {
        ArgumentNullException.ThrowIfNull(status);

        status.IsConverted = kind == LeadStatusKind.Converted;
        status.IsClosed = kind != LeadStatusKind.Open;

        // The default box is hidden, not cleared, when another type is picked, so it only counts for an open status.
        status.IsDefault = isDefault && kind == LeadStatusKind.Open;

        // A lead that was contacted moves out of the default status, so the contacted status is open and not the default.
        status.IsContacted = isContacted && kind == LeadStatusKind.Open && !status.IsDefault;
    }
}
