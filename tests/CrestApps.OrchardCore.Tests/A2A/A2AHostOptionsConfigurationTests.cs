using CrestApps.Core.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.A2A;

public sealed class A2AHostOptionsConfigurationTests
{
    [Fact]
    public void ConfigureServices_DefaultsAuthenticationTypeToOpenId_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        var services = new ServiceCollection();

        new A2AHostStartup(new MockShellConfiguration(configuration)).ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<A2AHostOptions>>().Value;

        Assert.Equal(A2AHostAuthenticationType.OpenId, options.AuthenticationType);
    }

    [Fact]
    public void ConfigureServices_PreservesExplicitAnonymousConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:A2AHost:AuthenticationType"] = "None",
            })
            .Build();
        var services = new ServiceCollection();

        new A2AHostStartup(new MockShellConfiguration(configuration)).ConfigureServices(services);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<A2AHostOptions>>().Value;

        Assert.Equal(A2AHostAuthenticationType.None, options.AuthenticationType);
    }

    private sealed class MockShellConfiguration : IShellConfiguration
    {
        private readonly IConfiguration _configuration;

        public MockShellConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);
    }
}
