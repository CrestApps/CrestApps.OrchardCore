using System.Text;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Providers;
using CrestApps.AI.Prompting.Rendering;

namespace CrestApps.AI.Prompting.Services;

/// <summary>
/// Default implementation of <see cref="IAITemplateService"/> that aggregates
/// templates from all registered <see cref="IAITemplateProvider"/>s.
/// </summary>
public class DefaultAITemplateService : IAITemplateService
{
    private readonly IEnumerable<IAITemplateProvider> _providers;
    private readonly IAITemplateEngine _renderer;

    public DefaultAITemplateService(
        IEnumerable<IAITemplateProvider> providers,
        IAITemplateEngine renderer)
    {
        _providers = providers;
        _renderer = renderer;
    }

    public virtual async Task<IReadOnlyList<AITemplate>> ListAsync()
    {
        var allTemplates = new List<AITemplate>();

        foreach (var provider in _providers)
        {
            var templates = await provider.GetTemplatesAsync();
            allTemplates.AddRange(templates);
        }

        return allTemplates;
    }

    public virtual async Task<AITemplate> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var allTemplates = await ListAsync();

        return allTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public virtual async Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
    {
        var template = await GetAsync(id)
            ?? throw new KeyNotFoundException($"AI template with ID '{id}' was not found.");

        return await _renderer.RenderAsync(template.Content, arguments);
    }

    public virtual async Task<string> MergeAsync(
        IEnumerable<string> ids,
        IDictionary<string, object> arguments = null,
        string separator = "\n\n")
    {
        ArgumentNullException.ThrowIfNull(ids);

        var builder = new StringBuilder();
        var isFirst = true;

        foreach (var id in ids)
        {
            var rendered = await RenderAsync(id, arguments);
            if (rendered == null)
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append(separator);
            }

            builder.Append(rendered);
            isFirst = false;
        }

        return builder.ToString();
    }
}
