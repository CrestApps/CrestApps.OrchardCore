using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

internal sealed class TestShellConfiguration : IShellConfiguration
{
    private readonly IConfiguration _configuration;

    public TestShellConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string this[string key]
    {
        get => _configuration[key];
        set => _configuration[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren()
        => _configuration.GetChildren();

    public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken()
        => _configuration.GetReloadToken();

    public IConfigurationSection GetSection(string key)
        => _configuration.GetSection(key);
}
