namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Resolves the <see cref="IWizardDefinition"/> instances registered by features. The generic wizard host
/// uses it to validate a requested wizard type before starting or resuming a session.
/// </summary>
public interface IWizardDefinitionProvider
{
    /// <summary>
    /// Returns every wizard definition contributed by the provider.
    /// </summary>
    /// <returns>The wizard definitions the provider contributes.</returns>
    IEnumerable<IWizardDefinition> GetDefinitions();
}
