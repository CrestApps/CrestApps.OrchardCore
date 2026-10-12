using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Wizard.Core;

/// <summary>
/// The permissions exposed by the wizard module.
/// </summary>
public static class WizardPermissions
{
    /// <summary>
    /// The permission required to start a new wizard session.
    /// </summary>
    public static readonly Permission StartWizard = new("StartWizard", "Start a wizard");

    /// <summary>
    /// The permission required to continue an existing wizard session.
    /// </summary>
    public static readonly Permission ContinueWizard = new("ContinueWizard", "Continue a wizard");
}
