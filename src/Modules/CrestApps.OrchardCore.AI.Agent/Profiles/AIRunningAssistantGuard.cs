using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.Agent.Profiles;

/// <summary>
/// Answers <see cref="IAIRunningAssistantGuard"/> with <see cref="AIConfigurationGuard"/>, for the tools of other modules.
/// </summary>
internal sealed class AIRunningAssistantGuard : IAIRunningAssistantGuard
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIRunningAssistantGuard"/> class.
    /// </summary>
    /// <param name="services">The services of the scope.</param>
    public AIRunningAssistantGuard(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public async Task<bool> IsProtectedDataSourceAsync(string dataSourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            return false;
        }

        var configuration = await AIConfigurationGuard.GetProtectedAsync(_services, cancellationToken);

        return configuration.IsProtectedDataSource(dataSourceId.Trim());
    }
}
