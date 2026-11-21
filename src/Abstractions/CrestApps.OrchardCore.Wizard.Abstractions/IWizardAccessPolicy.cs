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

    /// <summary>
    /// Returns whether a definition identifier names a wizard instance of the given type that can be started. A wizard
    /// started with a definition identifier that no policy recognizes answers "not found", so a visitor can't make up
    /// identifiers to create sessions without bound.
    /// </summary>
    /// <param name="wizardType">The wizard type discriminator being started.</param>
    /// <param name="definitionId">The definition identifier, which is never empty.</param>
    /// <returns><see langword="true"/> when the policy owns the definition and it exists, <see langword="false"/> when the
    /// policy owns the wizard type and the definition does not exist, or <see langword="null"/> when the policy has no
    /// opinion on the wizard type.</returns>
    Task<bool?> IsKnownDefinitionAsync(string wizardType, string definitionId)
        => Task.FromResult<bool?>(null);
}
