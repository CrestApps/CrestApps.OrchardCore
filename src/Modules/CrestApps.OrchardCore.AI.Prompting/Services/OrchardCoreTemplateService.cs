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
/// Templates from providers that are not <see cref="IDynamicTemplateProvider"/>s are cached
/// in memory for the lifetime of the tenant shell.
/// The <see cref="IMemoryCache"/> is scoped per tenant, so entries are
/// automatically cleared when the shell is released or the application restarts.
/// Dynamic providers, such as the one for System Prompt AI templates, are read once per scope,
/// so an edited template renders on the next request.
/// </summary>
public sealed class OrchardCoreTemplateService : DefaultTemplateService
{
    private const string CacheKey = "AITemplates_All";

    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreTemplateService"/> class.
    /// </summary>
    /// <param name="providers">The providers.</param>
    /// <param name="renderer">The renderer.</param>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="logger">The logger.</param>
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

    public override async Task<IReadOnlyList<Template>> ListAsync(CancellationToken cancellationToken = default)
    {
        var allTemplates = await base.ListAsync(cancellationToken);

        var enabledFeatures = await GetEnabledFeatureIdsAsync();

        return allTemplates
            .Where(t => string.IsNullOrEmpty(t.FeatureId) || enabledFeatures.Contains(t.FeatureId))
            .ToList();
    }

    public override async Task<Template> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var template = await base.GetAsync(id, cancellationToken);

        if (template == null)
        {
            _logger.LogWarning("template with ID '{TemplateId}' was not found.", id);
        }

        return template;
    }

    protected override async Task<IReadOnlyList<Template>> GetStaticTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue(CacheKey, out IReadOnlyList<Template> staticTemplates))
        {
            staticTemplates = await base.GetStaticTemplatesAsync(cancellationToken);
            _memoryCache.Set(CacheKey, staticTemplates);
        }

        return staticTemplates;
    }

    private async Task<HashSet<string>> GetEnabledFeatureIdsAsync()
    {
        var enabledFeatures = await _shellFeaturesManager.GetEnabledFeaturesAsync();

        return enabledFeatures
            .Select(f => f.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
