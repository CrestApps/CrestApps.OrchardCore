using CrestApps.OrchardCore.AI;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of the AI profile template manager.
/// </summary>
internal sealed class DefaultAIProfileTemplateManager : IAIProfileTemplateManager
{
    private readonly IEnumerable<IAIProfileTemplate> _templates;

    public DefaultAIProfileTemplateManager(IEnumerable<IAIProfileTemplate> templates)
    {
        _templates = templates;
    }

    public IEnumerable<IAIProfileTemplate> GetAllTemplates()
    {
        return _templates;
    }

    public IEnumerable<IAIProfileTemplate> GetTemplatesForSource(string profileSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileSource);

        return _templates.Where(t => string.IsNullOrEmpty(t.ProfileSource) ||
                                    string.Equals(t.ProfileSource, profileSource, StringComparison.OrdinalIgnoreCase));
    }

    public IAIProfileTemplate GetTemplate(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _templates.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
