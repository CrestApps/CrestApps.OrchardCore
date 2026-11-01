using CrestApps.Core.AI.WebMcp.Models;
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
    public void Configure_CopiesTheStoredToolSelectionIntoTheCoreOptions()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            ExposeAllTools = true,
            Tools = ["echo_tool", "tool_instance_docs"],
        };

        // Act
        var options = ConfigureTools(settings, []);

        // Assert
        Assert.True(options.ExposeAllTools);
        Assert.Equal(["echo_tool", "tool_instance_docs"], options.Tools);
    }

    [Fact]
    public void Configure_CopiesTheStoredPageKindsIntoThePageOptions()
    {
        // Arrange
        var settings = new WebMcpSettings
        {
            EnableOnAdminPages = false,
            EnableOnFrontEndPages = true,
        };

        // Act
        var options = ConfigurePages(settings, []);

        // Assert
        Assert.False(options.EnableOnAdminPages);
        Assert.True(options.EnableOnFrontEndPages);
        Assert.False(options.IsEnabledFor(isAdminPage: true));
        Assert.True(options.IsEnabledFor(isAdminPage: false));
        Assert.Empty(options.PageToolOrigins);
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
        var options = ConfigureTools(settings, []);

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
        var options = ConfigureTools(settings, []);
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

        var configuration = new Dictionary<string, string>
        {
            ["CrestApps:AI:WebMcp:EnableOnFrontEndPages"] = "true",
            ["CrestApps:AI:WebMcp:ExposeAllTools"] = "true",
            ["CrestApps:AI:WebMcp:PageToolOrigins:0"] = "https://partner.example",
        };

        // Act
        var pages = ConfigurePages(settings, configuration);
        var tools = ConfigureTools(settings, configuration);

        // Assert
        Assert.True(pages.EnableOnFrontEndPages);
        Assert.Equal(["https://partner.example"], pages.PageToolOrigins);
        Assert.True(tools.ExposeAllTools);
    }

    private static WebMcpOptions ConfigureTools(WebMcpSettings settings, Dictionary<string, string> configurationValues)
    {
        var options = new WebMcpOptions();

        CreateConfiguration(settings, configurationValues).Configure(options);

        return options;
    }

    private static WebMcpPageOptions ConfigurePages(WebMcpSettings settings, Dictionary<string, string> configurationValues)
    {
        var options = new WebMcpPageOptions();

        CreateConfiguration(settings, configurationValues).Configure(options);

        return options;
    }

    private static WebMcpOptionsConfiguration CreateConfiguration(WebMcpSettings settings, Dictionary<string, string> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new WebMcpOptionsConfiguration(
            SiteServiceFactory.Create(settings),
            new TestShellConfiguration(configuration));
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
