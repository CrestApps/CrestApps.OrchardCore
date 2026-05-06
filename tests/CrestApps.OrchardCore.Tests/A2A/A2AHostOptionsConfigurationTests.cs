using CrestApps.Core.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.Services;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.A2A;

public sealed class A2AHostOptionsConfigurationTests
{
    [Fact]
    public void Configure_DefaultsAuthenticationTypeToOpenId_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        var options = new A2AHostOptions();

        new A2AHostOptionsConfiguration(
            new MockShellConfiguration(configuration)).Configure(options);

        Assert.Equal(A2AHostAuthenticationType.OpenId, options.AuthenticationType);
    }

    [Fact]
    public void Configure_PreservesExplicitAnonymousConfiguration_FromNewPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:AI:A2AHost:AuthenticationType"] = "None",
            })
            .Build();
        var options = new A2AHostOptions();

        new A2AHostOptionsConfiguration(
            new MockShellConfiguration(configuration)).Configure(options);

        Assert.Equal(A2AHostAuthenticationType.None, options.AuthenticationType);
    }

    [Fact]
    public void Configure_PreservesExplicitAnonymousConfiguration_FromDeprecatedPath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:A2AHost:AuthenticationType"] = "None",
            })
            .Build();
        var options = new A2AHostOptions();

        new A2AHostOptionsConfiguration(
            new MockShellConfiguration(configuration)).Configure(options);

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
