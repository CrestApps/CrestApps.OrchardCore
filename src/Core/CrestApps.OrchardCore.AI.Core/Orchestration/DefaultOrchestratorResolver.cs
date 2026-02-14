using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Core.Orchestration;

/// <summary>
/// Resolves the appropriate <see cref="IOrchestrator"/> by name using the registered
/// orchestrator mappings from <see cref="OrchestratorOptions"/>.
/// Falls back to the system default when the requested name is not found.
/// </summary>
internal sealed class DefaultOrchestratorResolver : IOrchestratorResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly OrchestratorOptions _options;
    private readonly ILogger _logger;

    public DefaultOrchestratorResolver(
        IServiceProvider serviceProvider,
        IOptions<OrchestratorOptions> options,
        ILogger<DefaultOrchestratorResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public IOrchestrator Resolve(string orchestratorName = null)
    {
        var name = string.IsNullOrWhiteSpace(orchestratorName)
            ? _options.DefaultOrchestratorName
            : orchestratorName;

        if (_options.Orchestrators.TryGetValue(name, out var entry))
        {
            if (_serviceProvider.GetService(entry.Type) is IOrchestrator orchestrator)
            {
                return orchestrator;
            }

            _logger.LogWarning(
                "Orchestrator '{OrchestratorName}' (type {Type}) is registered but could not be resolved. Falling back to default.",
                name, entry.Type.Name);
        }
        else if (!string.IsNullOrWhiteSpace(orchestratorName))
        {
            _logger.LogWarning(
                "Orchestrator '{OrchestratorName}' is not registered. Falling back to default.",
                orchestratorName);
        }

        // Fall back to the default.
        if (!string.Equals(name, _options.DefaultOrchestratorName, StringComparison.OrdinalIgnoreCase) &&
            _options.Orchestrators.TryGetValue(_options.DefaultOrchestratorName, out var defaultEntry))
        {
            if (_serviceProvider.GetService(defaultEntry.Type) is IOrchestrator defaultOrchestrator)
            {
                return defaultOrchestrator;
            }
        }

        // Last resort: resolve ProgressiveToolOrchestrator directly.
        return _serviceProvider.GetRequiredService<ProgressiveToolOrchestrator>();
    }
}
