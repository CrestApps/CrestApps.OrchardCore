using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpOptionsConfigurationTests
{
    [Fact]
    public void Configure_CopiesTheStoredSettings()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            ExposeAllTools = true,
            Tools = ["echo_tool", "tool_instance_docs"],
            EnableOnAdminPages = false,
            EnableOnFrontEndPages = true,
        };

        // Act
        var options = Configure(settings, []);

        // Assert
        Assert.True(options.ExposeAllTools);
        Assert.Equal(["echo_tool", "tool_instance_docs"], options.Tools);
        Assert.False(options.EnableOnAdminPages);
        Assert.True(options.EnableOnFrontEndPages);
    }

    [Fact]
    public void Configure_TreatsAMissingToolListAsEmpty()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            Tools = null,
        };

        // Act
        var options = Configure(settings, []);

        // Assert
        Assert.NotNull(options.Tools);
        Assert.Empty(options.Tools);
    }

    [Fact]
    public void Configure_DoesNotShareTheStoredToolList()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            Tools = ["echo_tool"],
        };

        // Act
        var options = Configure(settings, []);
        options.Tools.Add("other");

        // Assert
        Assert.Equal(["echo_tool"], settings.Tools);
    }

    [Fact]
    public void Configure_LetsTheShellConfigurationOverrideTheStoredSettings()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            EnableOnFrontEndPages = false,
        };

        // Act
        var options = Configure(settings, new Dictionary<string, string>
        {
            ["CrestApps:AI:WebMcp:EnableOnFrontEndPages"] = "true",
            ["CrestApps:AI:WebMcp:ExposeAllTools"] = "true",
        });

        // Assert
        Assert.True(options.EnableOnFrontEndPages);
        Assert.True(options.ExposeAllTools);
    }

    private static WebMcpOptions Configure(WebMcpSettings settings, Dictionary<string, string> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var options = new WebMcpOptions();

        new WebMcpOptionsConfiguration(
            SiteServiceFactory.Create(settings),
            new TestShellConfiguration(configuration)).Configure(options);

        return options;
    }

    private sealed class TestShellConfiguration : IShellConfiguration
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

        public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren();

        public IChangeToken GetReloadToken() => _configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);
    }
}
