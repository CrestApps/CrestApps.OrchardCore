using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Configuration;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class McpServerOptionsConfigurationTests
{
    [Fact]
    public void Configure_DefaultsAuthenticationTypeToOpenId_WhenNotConfigured()
    {
        var options = Configure(new McpServerSettings(), []);

        Assert.Equal(McpServerAuthenticationType.OpenId, options.AuthenticationType);
    }

    [Fact]
    public void Configure_DoesNotExposeAnyToolsByDefault()
    {
        var options = Configure(new McpServerSettings(), []);

        Assert.False(options.ExposeAllTools);
        Assert.Empty(options.Tools);
    }

    [Fact]
    public void Configure_AppliesTheStoredToolAllowList()
    {
        var settings = new McpServerSettings
        {
            ExposeAllTools = false,
            Tools = ["crestapps-docs", "weather"],
        };

        var options = Configure(settings, []);

        Assert.False(options.ExposeAllTools);
        Assert.Equal(["crestapps-docs", "weather"], options.Tools);
    }

    [Fact]
    public void Configure_AppliesExposeAllToolsFromSettings()
    {
        var settings = new McpServerSettings
        {
            ExposeAllTools = true,
        };

        var options = Configure(settings, []);

        Assert.True(options.ExposeAllTools);
    }

    [Fact]
    public void Configure_PreservesExplicitAnonymousConfiguration_FromNewPath()
    {
        var options = Configure(new McpServerSettings(), new Dictionary<string, string>
        {
            ["CrestApps:AI:McpServer:AuthenticationType"] = "None",
        });

        Assert.Equal(McpServerAuthenticationType.None, options.AuthenticationType);
    }

    [Fact]
    public void Configure_PreservesExplicitAnonymousConfiguration_FromDeprecatedPath()
    {
        var options = Configure(new McpServerSettings(), new Dictionary<string, string>
        {
            ["CrestApps:McpServer:AuthenticationType"] = "None",
        });

        Assert.Equal(McpServerAuthenticationType.None, options.AuthenticationType);
    }

    [Fact]
    public void Configure_ShellConfigurationOverridesStoredSettings()
    {
        var settings = new McpServerSettings
        {
            AuthenticationType = McpServerAuthenticationType.OpenId,
        };

        var options = Configure(settings, new Dictionary<string, string>
        {
            ["CrestApps:AI:McpServer:AuthenticationType"] = "ApiKey",
        });

        Assert.Equal(McpServerAuthenticationType.ApiKey, options.AuthenticationType);
    }

    private static McpServerOptions Configure(McpServerSettings settings, Dictionary<string, string> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var options = new McpServerOptions();

        new McpServerOptionsConfiguration(
            SiteServiceFactory.Create(settings),
            new MockShellConfiguration(configuration)).Configure(options);

        return options;
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
