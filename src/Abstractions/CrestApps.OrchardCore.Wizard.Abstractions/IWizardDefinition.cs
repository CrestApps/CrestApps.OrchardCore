namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Declares a wizard that can be started and navigated through the generic wizard host. A definition is the
/// contract a code consumer registers so the public controller can validate the requested wizard type,
/// enforce access, and know how to finalize and confirm the wizard.
/// </summary>
public interface IWizardDefinition
{
    /// <summary>
    /// Gets the wizard type discriminator this definition describes. It must be unique across all registered
    /// definitions and is used to correlate sessions, handlers, and step drivers.
    /// </summary>
    string WizardType { get; }

    /// <summary>
    /// Gets the human-readable display name of the wizard.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether the wizard can only be started by an authenticated user.
    /// </summary>
    bool RequiresAuthenticatedUser { get; }
}
