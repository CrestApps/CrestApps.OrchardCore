using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Providers;
using CrestApps.Core.Templates.Rendering;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Prompting.Services;

/// <summary>
/// OrchardCore-aware prompt template service that filters templates
/// based on enabled features in the current shell and caches parsed templates.
/// Templates are cached in memory for the lifetime of the tenant shell.
/// The <see cref="IMemoryCache"/> is scoped per tenant, so entries are
/// automatically cleared when the shell is released or the application restarts.
/// </summary>
public sealed class OrchardCoreTemplateService : DefaultTemplateService
{
    private const string CacheKey = "AITemplates_All";

    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;

    public OrchardCoreTemplateService(
        IEnumerable<ITemplateProvider> providers,
        ITemplateEngine renderer,
        IShellFeaturesManager shellFeaturesManager,
        IMemoryCache memoryCache,
        ILogger<OrchardCoreTemplateService> logger)
    : base(providers, renderer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public override async Task<IReadOnlyList<Template>> ListAsync()
    {
        if (!_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<Template> allTemplates))
        {
            allTemplates = await base.ListAsync();
            _memoryCache.Set(CacheKey, allTemplates);
        }

        var enabledFeatures = await GetEnabledFeatureIdsAsync();

        var filteredTemplates = allTemplates
            .Where(t => string.IsNullOrEmpty(t.FeatureId) || enabledFeatures.Contains(t.FeatureId));

        var deduplicatedTemplates = new List<Template>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in filteredTemplates)
        {
            if (string.IsNullOrWhiteSpace(template.Id) || !seenIds.Add(template.Id))
            {
                continue;
            }

            deduplicatedTemplates.Add(template);
        }

        return deduplicatedTemplates;
    }

    public override async Task<Template> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var allTemplates = await ListAsync();

        var template = allTemplates.FirstOrDefault(t =>
        string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        if (template == null)
        {
            _logger.LogWarning("template with ID '{TemplateId}' was not found. There are {Count} templates available.", id, allTemplates.Count);
        }

        return template;
    }

    private async Task<HashSet<string>> GetEnabledFeatureIdsAsync()
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();

        return enabledFeatures
            .Select(f => f.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
