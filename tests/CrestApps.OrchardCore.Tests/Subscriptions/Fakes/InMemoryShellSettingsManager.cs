using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// Keeps tenant settings in memory the way <c>ShellSettingsManager</c> keeps them in <c>tenants.json</c>: a saved tenant
/// loads back as saved, and a name that was never saved loads as an uninitialized tenant with no host and no prefix,
/// which is what Orchard Core's own manager builds from the default configuration.
/// </summary>
internal sealed class InMemoryShellSettingsManager : IShellSettingsManager
{
    private readonly Dictionary<string, ShellSettings> _saved = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ShellSettings> Saved => _saved;

    public ShellSettings CreateDefaultSettings()
        => new();

    public Task<IEnumerable<ShellSettings>> LoadSettingsAsync()
        => Task.FromResult<IEnumerable<ShellSettings>>([.. _saved.Values.Select(settings => new ShellSettings(settings))]);

    public Task<IEnumerable<string>> LoadSettingsNamesAsync()
        => Task.FromResult<IEnumerable<string>>([.. _saved.Keys]);

    public Task<ShellSettings> LoadSettingsAsync(string tenant)
    {
        if (_saved.TryGetValue(tenant, out var settings))
        {
            return Task.FromResult(new ShellSettings(settings));
        }

        var unsaved = new ShellSettings { Name = tenant };
        unsaved.AsUninitialized();

        return Task.FromResult(unsaved);
    }

    public Task SaveSettingsAsync(ShellSettings settings)
    {
        _saved[settings.Name] = new ShellSettings(settings);

        return Task.CompletedTask;
    }

    public Task RemoveSettingsAsync(ShellSettings settings)
    {
        _saved.Remove(settings.Name);

        return Task.CompletedTask;
    }
}
