using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using OrchardCore.Scripting;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AITemplateMethodProvider : IGlobalMethodProvider
{
    private readonly INamedCatalogManager<AIProfileTemplate> _templateManager;
    private readonly GlobalMethod _globalMethod;

    public AITemplateMethodProvider(INamedCatalogManager<AIProfileTemplate> templateManager)
    {
        _templateManager = templateManager;

        _globalMethod = new GlobalMethod
        {
            Name = "ai_template",
            Method = _ => (Func<string, object>)(templateId => ResolveTemplateContentAsync(templateId).GetAwaiter().GetResult()),
            AsyncMethod = _ => (Func<string, Task<object>>)(ResolveTemplateContentAsync),
        };
    }

    public IEnumerable<GlobalMethod> GetMethods()
    {
        yield return _globalMethod;
    }

    private async Task<object> ResolveTemplateContentAsync(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return string.Empty;
        }

        var normalizedTemplateId = templateId.Trim();

        var template = await _templateManager.FindByIdAsync(normalizedTemplateId)
            ?? await _templateManager.FindByNameAsync(normalizedTemplateId);

        if (template is null)
        {
            return string.Empty;
        }

        if (template.Source == AITemplateSources.SystemPrompt)
        {
            return template.GetOrCreate<SystemPromptTemplateMetadata>().SystemMessage ?? string.Empty;
        }

        return template.GetOrCreate<ProfileTemplateMetadata>().SystemMessage ?? string.Empty;
    }
}
