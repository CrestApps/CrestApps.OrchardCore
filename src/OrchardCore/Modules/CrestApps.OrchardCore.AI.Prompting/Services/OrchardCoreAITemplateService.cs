using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Providers;
using CrestApps.AI.Prompting.Rendering;
using CrestApps.AI.Prompting.Services;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Prompting.Services;

/// <summary>
/// OrchardCore-aware prompt template service that filters templates
/// based on enabled features in the current shell and caches parsed templates.
/// Templates are cached in memory for the lifetime of the tenant shell.
/// The <see cref="IMemoryCache"/> is scoped per tenant, so entries are
/// automatically cleared when the shell is released or the application restarts.
/// </summary>
public sealed class OrchardCoreAITemplateService : DefaultAITemplateService
{
    private const string CacheKey = "AITemplates_All";

    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IMemoryCache _memoryCache;

    public OrchardCoreAITemplateService(
        IEnumerable<IAITemplateProvider> providers,
        IAITemplateEngine renderer,
        IShellFeaturesManager shellFeaturesManager,
        IMemoryCache memoryCache)
        : base(providers, renderer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _memoryCache = memoryCache;
    }

    public override async Task<IReadOnlyList<AITemplate>> ListAsync()
    {
        if (!_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<AITemplate> allTemplates))
        {
            allTemplates = await base.ListAsync();
            _memoryCache.Set(CacheKey, allTemplates);
        }

        var enabledFeatures = await GetEnabledFeatureIdsAsync();

        return allTemplates
            .Where(t => string.IsNullOrEmpty(t.FeatureId) || enabledFeatures.Contains(t.FeatureId))
            .ToList();
    }

    public override async Task<AITemplate> GetAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var allTemplates = await ListAsync();

        return allTemplates.FirstOrDefault(t =>
            string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<HashSet<string>> GetEnabledFeatureIdsAsync()
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();

        return enabledFeatures
            .Select(f => f.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
