using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Core;

/// <summary>
/// Defines the permissions exposed by the base Contact Center feature.
/// </summary>
public static class ContactCenterPermissions
{
    /// <summary>
    /// Grants full management of the Contact Center, including configuration and every interaction.
    /// </summary>
    public static readonly Permission ManageContactCenter = new("ManageContactCenter", "Manage the Contact Center");

    /// <summary>
    /// Grants management of interactions.
    /// </summary>
    public static readonly Permission ManageInteractions = new("ManageInteractions", "Manage interactions", [ManageContactCenter]);

    /// <summary>
    /// Grants read-only access to interactions.
    /// </summary>
    public static readonly Permission ViewInteractions = new("ViewInteractions", "View interactions", [ManageInteractions, ManageContactCenter]);
}
