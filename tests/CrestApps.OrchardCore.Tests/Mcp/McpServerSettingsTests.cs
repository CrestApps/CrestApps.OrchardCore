using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Models;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class McpServerSettingsTests
{
    [Fact]
    public void Defaults_DoNotExposeAnyTools()
    {
        var settings = new McpServerSettings();

        Assert.False(settings.ExposeAllTools);
        Assert.NotNull(settings.Tools);
        Assert.Empty(settings.Tools);
    }

    [Fact]
    public void Defaults_UseOpenIdWithRequiredAccessPermission()
    {
        var settings = new McpServerSettings();

        Assert.Equal(McpServerAuthenticationType.OpenId, settings.AuthenticationType);
        Assert.True(settings.RequireAccessPermission);
    }
}
