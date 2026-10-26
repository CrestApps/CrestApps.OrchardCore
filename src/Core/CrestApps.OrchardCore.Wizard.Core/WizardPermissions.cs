using OrchardCore.Localization;
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
    public static readonly Permission StartWizard = new("StartWizard", LocalizationSource.Create("Start a wizard", typeof(WizardPermissions)));

    /// <summary>
    /// The permission required to continue an existing wizard session.
    /// </summary>
    public static readonly Permission ContinueWizard = new("ContinueWizard", LocalizationSource.Create("Continue a wizard", typeof(WizardPermissions)));
}
