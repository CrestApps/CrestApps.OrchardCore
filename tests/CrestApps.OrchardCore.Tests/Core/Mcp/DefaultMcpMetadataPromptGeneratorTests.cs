using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class DefaultMcpMetadataPromptGeneratorTests
{
    private readonly DefaultMcpMetadataPromptGenerator _generator = new();

    [Fact]
    public void Generate_WithNullCapabilities_ReturnsNull()
    {
        var result = _generator.Generate(null);

        Assert.Null(result);
    }

    [Fact]
    public void Generate_WithEmptyCapabilities_ReturnsNull()
    {
        var result = _generator.Generate([]);

        Assert.Null(result);
    }

    [Fact]
    public void Generate_WithNoActualCapabilities_ReturnsNull()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn1",
                ConnectionDisplayText = "Server 1",
                Tools = [],
                Prompts = [],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.Null(result);
    }

    [Fact]
    public void Generate_WithTools_IncludesToolsSection()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn1",
                ConnectionDisplayText = "My Server",
                Tools =
                [
                    new McpServerCapability
                    {
                        Name = "search",
                        Description = "Search the web",
                    },
                ],
                Prompts = [],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("mcp_invoke", result);
        Assert.Contains("My Server", result);
        Assert.Contains("conn1", result);
        Assert.Contains("Tools (pass required arguments via 'inputs'):", result);
        Assert.Contains("search", result);
        Assert.Contains("Search the web", result);
    }

    [Fact]
    public void Generate_WithPrompts_IncludesPromptsSection()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn1",
                ConnectionDisplayText = "Server",
                Tools = [],
                Prompts =
                [
                    new McpServerCapability
                    {
                        Name = "summarize",
                        Description = "Summarize text",
                    },
                ],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("Prompts:", result);
        Assert.Contains("summarize", result);
        Assert.Contains("Summarize text", result);
    }

    [Fact]
    public void Generate_WithResources_IncludesResourcesSection()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn1",
                ConnectionDisplayText = "Server",
                Tools = [],
                Prompts = [],
                Resources =
                [
                    new McpServerCapability
                    {
                        Name = "docs",
                        Description = "Documentation files",
                        Uri = "file://docs/readme.md",
                    },
                ],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("Resources (use the URI as 'id' when invoking):", result);
        Assert.Contains("file://docs/readme.md", result);
        Assert.Contains("Documentation files", result);
    }

    [Fact]
    public void Generate_WithMultipleServers_IncludesAllServers()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn1",
                ConnectionDisplayText = "Server A",
                Tools =
                [
                    new McpServerCapability { Name = "toolA" },
                ],
                Prompts = [],
                Resources = [],
            },
            new()
            {
                ConnectionId = "conn2",
                ConnectionDisplayText = "Server B",
                Tools =
                [
                    new McpServerCapability { Name = "toolB" },
                ],
                Prompts = [],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("Server A", result);
        Assert.Contains("conn1", result);
        Assert.Contains("toolA", result);
        Assert.Contains("Server B", result);
        Assert.Contains("conn2", result);
        Assert.Contains("toolB", result);
    }

    [Fact]
    public void Generate_SkipsEmptyServers()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "empty",
                ConnectionDisplayText = "Empty Server",
                Tools = [],
                Prompts = [],
                Resources = [],
            },
            new()
            {
                ConnectionId = "active",
                ConnectionDisplayText = "Active Server",
                Tools =
                [
                    new McpServerCapability { Name = "myTool" },
                ],
                Prompts = [],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.DoesNotContain("Empty Server", result);
        Assert.Contains("Active Server", result);
    }

    [Fact]
    public void Generate_UsesConnectionIdWhenDisplayTextIsNull()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "conn-id-123",
                ConnectionDisplayText = null,
                Tools =
                [
                    new McpServerCapability { Name = "tool1" },
                ],
                Prompts = [],
                Resources = [],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("conn-id-123", result);
    }

    [Fact]
    public void Generate_WithAllCapabilityTypes_IncludesAllSections()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            new()
            {
                ConnectionId = "full",
                ConnectionDisplayText = "Full Server",
                Tools =
                [
                    new McpServerCapability { Name = "calc" },
                ],
                Prompts =
                [
                    new McpServerCapability { Name = "greet" },
                ],
                Resources =
                [
                    new McpServerCapability { Name = "data", Uri = "file://data" },
                ],
            },
        };

        var result = _generator.Generate(capabilities);

        Assert.NotNull(result);
        Assert.Contains("Tools (pass required arguments via 'inputs'):", result);
        Assert.Contains("calc", result);
        Assert.Contains("Prompts:", result);
        Assert.Contains("greet", result);
        Assert.Contains("Resources (use the URI as 'id' when invoking):", result);
        Assert.Contains("file://data", result);
    }
}
