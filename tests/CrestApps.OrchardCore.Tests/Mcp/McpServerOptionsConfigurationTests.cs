using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class McpServerOptionsConfigurationTests
{
    [Fact]
    public void Configure_DefaultsAuthenticationTypeToOpenId_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();
        var options = new McpServerOptions();

        new McpServerOptionsConfiguration(new MockShellConfiguration(configuration)).Configure(options);

        Assert.Equal(McpServerAuthenticationType.OpenId, options.AuthenticationType);
    }

    [Fact]
    public void Configure_PreservesExplicitAnonymousConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["CrestApps:McpServer:AuthenticationType"] = "None",
            })
            .Build();
        var options = new McpServerOptions();

        new McpServerOptionsConfiguration(new MockShellConfiguration(configuration)).Configure(options);

        Assert.Equal(McpServerAuthenticationType.None, options.AuthenticationType);
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
