using CrestApps.AI.Prompting.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Prompting.Providers;

/// <summary>
/// Provides prompt templates registered via code using <see cref="AITemplateOptions"/>.
/// </summary>
public sealed class OptionsAITemplateProvider : IAITemplateProvider
{
    private readonly AITemplateOptions _options;

    public OptionsAITemplateProvider(IOptions<AITemplateOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        return Task.FromResult<IReadOnlyList<AITemplate>>(_options.Templates.ToList());
    }
}
