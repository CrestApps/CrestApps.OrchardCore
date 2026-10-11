namespace CrestApps.OrchardCore.Wizard.Core.Services;

/// <summary>
/// The default <see cref="IWizardDefinitionProvider"/> that aggregates every <see cref="IWizardDefinition"/>
/// registered in the container.
/// </summary>
public sealed class DefaultWizardDefinitionProvider : IWizardDefinitionProvider
{
    private readonly IEnumerable<IWizardDefinition> _definitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultWizardDefinitionProvider"/> class.
    /// </summary>
    /// <param name="definitions">The registered wizard definitions.</param>
    public DefaultWizardDefinitionProvider(IEnumerable<IWizardDefinition> definitions)
    {
        _definitions = definitions;
    }

    /// <inheritdoc/>
    public IEnumerable<IWizardDefinition> GetDefinitions()
        => _definitions;
}
