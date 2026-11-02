using CrestApps.OrchardCore.AI.WebMcp.Drivers;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpPartDisplayDriverTests
{
    [Fact]
    public void BuildElementId_KeepsOnlyLettersAndDigits()
    {
        // Act
        var elementId = WebMcpPartDisplayDriver.BuildElementId("4abc'\"<x>-9");

        // Assert
        Assert.Equal("crestapps-webmcp-content-4abcx9", elementId);
    }

    [Fact]
    public void BuildConfigurationElement_WritesAJsonBlockThePageCannotRun()
    {
        // Arrange
        var configuration = new Dictionary<string, object>
        {
            ["toolsUrl"] = "/api/ai/webmcp/content-items/abc/tools",
            ["antiforgeryToken"] = "</script><script>alert(1)</script>",
        };

        // Act
        var element = WebMcpPartDisplayDriver.BuildConfigurationElement("crestapps-webmcp-content-abc", configuration);

        // Assert
        Assert.StartsWith("<script type=\"application/json\" id=\"crestapps-webmcp-content-abc\">", element, StringComparison.Ordinal);
        Assert.EndsWith("</script>", element, StringComparison.Ordinal);
        Assert.Single(element.Split("</script>", StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void BuildRegistrationScript_RegistersTheItemToolsWithTheStandardApi()
    {
        // Act
        var script = WebMcpPartDisplayDriver.BuildRegistrationScript("crestapps-webmcp-content-abc");

        // Assert
        Assert.Contains("document.getElementById('crestapps-webmcp-content-abc')", script, StringComparison.Ordinal);
        Assert.Contains("window.CrestAppsWebMcp.registerServerTools(document.modelContext", script, StringComparison.Ordinal);
        Assert.Contains("!document.modelContext", script, StringComparison.Ordinal);
    }
}
