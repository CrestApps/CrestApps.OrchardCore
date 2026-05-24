using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Scripting;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AITemplateMethodProvider : IGlobalMethodProvider
{
    private readonly INamedCatalogManager<AIProfileTemplate> _templateManager;
    private readonly ILogger<AITemplateMethodProvider> _logger;
    private readonly GlobalMethod _globalMethod;

    public AITemplateMethodProvider(
        INamedCatalogManager<AIProfileTemplate> templateManager,
        ILogger<AITemplateMethodProvider> logger)
    {
        _templateManager = templateManager;
        _logger = logger;

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
            _logger.LogWarning("Unable to resolve AI template '{TemplateIdOrName}' by id or name.", normalizedTemplateId);
            return string.Empty;
        }

        if (template.Source == AITemplateSources.SystemPrompt)
        {
            var systemMessage = template.GetOrCreate<SystemPromptTemplateMetadata>().SystemMessage;

            if (string.IsNullOrWhiteSpace(systemMessage))
            {
                _logger.LogWarning("AI template '{TemplateIdOrName}' has no system message content.", normalizedTemplateId);
                return string.Empty;
            }

            return systemMessage;
        }

        var profileSystemMessage = template.GetOrCreate<ProfileTemplateMetadata>().SystemMessage;

        if (string.IsNullOrWhiteSpace(profileSystemMessage))
        {
            _logger.LogWarning("AI template '{TemplateIdOrName}' has no system message content.", normalizedTemplateId);
            return string.Empty;
        }

        return profileSystemMessage;
    }
}
