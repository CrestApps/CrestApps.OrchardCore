using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.AspNetCore;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class McpHttpTransportOptionsConfigurationTests
{
    [Fact]
    public void Configure_KeepsSessionsForInitializeClients_WhenNotConfigured()
    {
        var options = Configure(null);

        Assert.Equal(HttpServerSessionMode.StatefulForInitializeClients, options.SessionMode);

        // A stateless transport does not map GET and DELETE /mcp, which answered 405 (issue #1344).
        Assert.False(options.Stateless);
    }

    [Theory]
    [InlineData("Stateless", HttpServerSessionMode.Stateless)]
    [InlineData("stateful", HttpServerSessionMode.Stateful)]
    [InlineData("StatefulForInitializeClients", HttpServerSessionMode.StatefulForInitializeClients)]
    public void Configure_UsesTheConfiguredSessionMode(string value, HttpServerSessionMode expected)
    {
        var options = Configure(value);

        Assert.Equal(expected, options.SessionMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sticky")]
    [InlineData("42")]
    public void Configure_FallsBackToSessionsForInitializeClients_WhenTheValueIsUnknown(string value)
    {
        var options = Configure(value);

        Assert.Equal(HttpServerSessionMode.StatefulForInitializeClients, options.SessionMode);
    }

    private static HttpServerTransportOptions Configure(string sessionMode)
    {
        var values = new Dictionary<string, string>();

        if (sessionMode is not null)
        {
            values[McpHttpTransportOptionsConfiguration.SessionModeKey] = sessionMode;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new HttpServerTransportOptions();

        new McpHttpTransportOptionsConfiguration(new MockShellConfiguration(configuration)).Configure(options);

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
