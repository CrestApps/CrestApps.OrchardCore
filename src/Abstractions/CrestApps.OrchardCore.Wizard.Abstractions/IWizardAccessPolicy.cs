namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Contributes per-instance access rules for a wizard so a feature can, for example, require an
/// authenticated visitor for a specific wizard definition that the type-level <see cref="IWizardDefinition"/>
/// cannot express on its own.
/// </summary>
public interface IWizardAccessPolicy
{
    /// <summary>
    /// Returns whether the wizard identified by the given type and definition requires an authenticated
    /// visitor before it can be started.
    /// </summary>
    /// <param name="wizardType">The wizard type discriminator being started.</param>
    /// <param name="definitionId">The optional definition identifier that distinguishes the specific wizard instance.</param>
    /// <returns><see langword="true"/> when an authenticated visitor is required; otherwise, <see langword="false"/>.</returns>
    Task<bool> RequiresAuthenticatedUserAsync(string wizardType, string definitionId);
}
