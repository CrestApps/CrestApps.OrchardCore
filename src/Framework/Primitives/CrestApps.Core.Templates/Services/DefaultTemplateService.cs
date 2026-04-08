using System.Text;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Providers;
using CrestApps.Core.Templates.Rendering;

namespace CrestApps.Core.Templates.Services;

/// <summary>
/// Default implementation of <see cref="ITemplateService"/> that aggregates
/// templates from all registered <see cref="ITemplateProvider"/>s.
/// </summary>
public class DefaultTemplateService : ITemplateService
{
    private readonly IEnumerable<ITemplateProvider> _providers;
    private readonly ITemplateEngine _renderer;

    public DefaultTemplateService(
        IEnumerable<ITemplateProvider> providers,
        ITemplateEngine renderer)
    {
        _providers = providers;
        _renderer = renderer;
    }

    public virtual async Task<IReadOnlyList<Template>> ListAsync()
    {
        var allTemplates = new List<Template>();

        foreach (var provider in _providers)
        {
            var templates = await provider.GetTemplatesAsync();
            allTemplates.AddRange(templates);
        }

        return allTemplates;
    }

    public virtual async Task<Template> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var allTemplates = await ListAsync();

        return allTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public virtual async Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
    {
        var template = await GetAsync(id)
            ?? throw new KeyNotFoundException($"template with ID '{id}' was not found.");

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
