using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class DefaultAIProfileTemplateService : IAIProfileTemplateService
{
    private readonly INamedCatalog<AIProfileTemplate> _catalog;
    private readonly IEnumerable<IAIProfileTemplateProvider> _providers;

    public DefaultAIProfileTemplateService(
        INamedCatalog<AIProfileTemplate> catalog,
        IEnumerable<IAIProfileTemplateProvider> providers)
    {
        _catalog = catalog;
        _providers = providers;
    }

    public async Task<IReadOnlyList<AIProfileTemplate>> GetAllAsync()
    {
        var templates = new List<AIProfileTemplate>();

        // Add DB-stored templates.
        var dbTemplates = await _catalog.GetAllAsync();
        templates.AddRange(dbTemplates);

        // Add file-based templates (avoiding duplicates by name).
        var existingNames = new HashSet<string>(templates.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var fileTemplates = await provider.GetTemplatesAsync();

            foreach (var template in fileTemplates)
            {
                if (!existingNames.Contains(template.Name))
                {
                    templates.Add(template);
                    existingNames.Add(template.Name);
                }
            }
        }

        return templates;
    }

    public async Task<IReadOnlyList<AIProfileTemplate>> GetListableAsync()
    {
        var all = await GetAllAsync();

        return all.Where(t => t.IsListable).ToList();
    }

    public async Task<AIProfileTemplate> FindByIdAsync(string id)
    {
        // First, try DB store.
        var template = await _catalog.FindByIdAsync(id);

        if (template != null)
        {
            return template;
        }

        // Then search file-based providers.
        foreach (var provider in _providers)
        {
            var templates = await provider.GetTemplatesAsync();
            template = templates.FirstOrDefault(t => string.Equals(t.ItemId, id, StringComparison.OrdinalIgnoreCase));

            if (template != null)
            {
                return template;
            }
        }

        return null;
    }
}
