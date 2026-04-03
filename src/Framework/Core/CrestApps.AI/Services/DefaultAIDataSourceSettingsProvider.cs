using CrestApps.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Services;

/// <summary>
/// Default options-backed implementation of <see cref="IAIDataSourceSettingsProvider"/>.
/// Hosts can replace this with a tenant-aware implementation.
/// </summary>
public sealed class DefaultAIDataSourceSettingsProvider : IAIDataSourceSettingsProvider
{
    private readonly IOptionsSnapshot<AIDataSourceSettings> _settings;

    public DefaultAIDataSourceSettingsProvider(IOptionsSnapshot<AIDataSourceSettings> settings)
    {
        _settings = settings;
    }

    public Task<AIDataSourceSettings> GetAsync() => Task.FromResult(_settings.Value);
}
