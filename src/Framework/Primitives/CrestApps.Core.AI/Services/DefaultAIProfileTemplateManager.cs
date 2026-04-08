using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Services;

public sealed class DefaultAIProfileTemplateManager : NamedSourceCatalogManager<AIProfileTemplate>, IAIProfileTemplateManager
{
    private readonly IEnumerable<IAIProfileTemplateProvider> _providers;

    public DefaultAIProfileTemplateManager(
        INamedSourceCatalog<AIProfileTemplate> catalog,
        IEnumerable<ICatalogEntryHandler<AIProfileTemplate>> handlers,
        IEnumerable<IAIProfileTemplateProvider> providers,
        ILogger<DefaultAIProfileTemplateManager> logger)
        : base(catalog, handlers, logger)
    {
        _providers = providers;
    }

    public new async ValueTask<IEnumerable<AIProfileTemplate>> GetAllAsync()
    {
        var dbTemplates = await base.GetAllAsync();

        return await MergeWithProvidersAsync(dbTemplates);
    }

    public new async ValueTask<AIProfileTemplate> FindByIdAsync(string id)
    {
        var template = await base.FindByIdAsync(id);

        if (template is not null)
        {
            return template;
        }

        foreach (var provider in _providers)
        {
            var templates = await provider.GetTemplatesAsync();
            template = templates.FirstOrDefault(t => string.Equals(t.ItemId, id, StringComparison.OrdinalIgnoreCase));

            if (template is not null)
            {
                return template;
            }
        }

        return null;
    }

    public new async ValueTask<IEnumerable<AIProfileTemplate>> GetAsync(string source)
    {
        var dbTemplates = await base.GetAsync(source);

        return await MergeWithProvidersAsync(dbTemplates, source);
    }

    public new async ValueTask<IEnumerable<AIProfileTemplate>> FindBySourceAsync(string source)
    {
        var dbTemplates = await base.FindBySourceAsync(source);

        return await MergeWithProvidersAsync(dbTemplates, source);
    }

    public async ValueTask<IEnumerable<AIProfileTemplate>> GetListableAsync()
    {
        var templates = await GetAllAsync();

        return templates.Where(template => template.IsListable);
    }

    private async Task<IEnumerable<AIProfileTemplate>> MergeWithProvidersAsync(IEnumerable<AIProfileTemplate> dbTemplates, string source = null)
    {
        var templates = new List<AIProfileTemplate>(dbTemplates);
        var existingNames = new HashSet<string>(templates.Select(template => template.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var provider in _providers)
        {
            var providerTemplates = await provider.GetTemplatesAsync();

            foreach (var template in providerTemplates)
            {
                if (existingNames.Contains(template.Name))
                {
                    continue;
                }

                if (source is not null && !string.Equals(template.Source, source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                templates.Add(template);
                existingNames.Add(template.Name);
            }
        }

        return templates;
    }
}
