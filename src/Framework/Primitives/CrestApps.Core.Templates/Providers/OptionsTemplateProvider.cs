using CrestApps.Core.Templates.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Templates.Providers;

/// <summary>
/// Provides prompt templates registered via code using <see cref="TemplateOptions"/>.
/// </summary>
public sealed class OptionsTemplateProvider : ITemplateProvider
{
    private readonly TemplateOptions _options;
    public OptionsTemplateProvider(IOptions<TemplateOptions> options)
    {
        _options = options.Value;
    }

    public Task<IReadOnlyList<Template>> GetTemplatesAsync()
    {
        return Task.FromResult<IReadOnlyList<Template>>(_options.Templates.ToList());
    }
}
