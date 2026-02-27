using CrestApps.AI.Prompting.Rendering;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

/// <summary>
/// Verifies that Liquid templates can access typed .NET object properties
/// through the <see cref="FluidAITemplateEngine"/>'s UnsafeMemberAccessStrategy.
/// Each test renders a real template pattern with sample objects.
/// </summary>
public sealed class TemplateLiquidRenderingTests
{
    private readonly FluidAITemplateEngine _engine;

    public TemplateLiquidRenderingTests()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        _engine = new FluidAITemplateEngine(
            services,
            NullLogger<FluidAITemplateEngine>.Instance);
    }

    [Fact]
    public async Task DocumentAvailability_WithToolsAndDocuments_RendersCorrectly()
    {
        var template = """
            {% if tools.size > 0 %}
            Available document tools:
            {% for tool in tools %}
            - {{ tool.Name }}: {{ tool.Description }}
            {% endfor %}
            {% endif %}
            {% if availableDocuments.size > 0 %}
            {% for doc in availableDocuments %}
            - {{ doc.DocumentId }}: "{{ doc.FileName }}" ({{ doc.ContentType | default: "unknown" }}, {{ doc.FileSize }} bytes)
            {% endfor %}
            {% endif %}
            """;

        var tools = new[]
        {
            new AIToolDefinitionEntry(typeof(object))
            {
                Name = "read_document",
                Description = "Reads document content",
                Purpose = AIToolPurposes.DocumentProcessing,
            },
        };

        var docs = new[]
        {
            new ChatInteractionDocumentInfo
            {
                DocumentId = "doc1",
                FileName = "report.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
            },
            new ChatInteractionDocumentInfo
            {
                DocumentId = "doc2",
                FileName = "data.csv",
                ContentType = "text/csv",
                FileSize = 512,
            },
        };

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = tools,
            ["availableDocuments"] = docs,
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("read_document", result);
        Assert.Contains("Reads document content", result);
        Assert.Contains("doc1", result);
        Assert.Contains("report.pdf", result);
        Assert.Contains("application/pdf", result);
        Assert.Contains("2048", result);
        Assert.Contains("doc2", result);
        Assert.Contains("data.csv", result);
    }

    [Fact]
    public async Task DocumentAvailability_NoTools_ShowsFallbackMessage()
    {
        var template = """
            {% if tools.size > 0 %}
            Available document tools:
            {% for tool in tools %}
            - {{ tool.Name }}
            {% endfor %}
            {% else %}
            The user has uploaded documents as supplementary context.
            {% endif %}
            """;

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = Array.Empty<AIToolDefinitionEntry>(),
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("supplementary context", result);
        Assert.DoesNotContain("Available document tools", result);
    }

    [Fact]
    public async Task TaskPlanning_WithToolRegistryEntries_RendersCorrectly()
    {
        var template = """
            {% assign hasUserTools = false %}{% assign hasSystemTools = false %}
            {% for tool in tools %}{% if tool.Source == "Local" %}{% assign hasUserTools = true %}{% endif %}{% if tool.Source == "System" %}{% assign hasSystemTools = true %}{% endif %}{% endfor %}
            {% if hasUserTools %}
            User tools:
            {% for tool in tools %}{% if tool.Source == "Local" %}
            - {{ tool.Name }}{% if tool.Description %}: {{ tool.Description }}{% endif %}
            {% endif %}{% endfor %}
            {% endif %}
            {% if hasSystemTools %}
            System tools:
            {% for tool in tools %}{% if tool.Source == "System" %}
            - {{ tool.Name }}{% if tool.Description %}: {{ tool.Description }}{% endif %}
            {% endif %}{% endfor %}
            {% endif %}
            """;

        // Fluid renders enums as integers, so Source must be projected to string.
        var tools = new object[]
        {
            new { Name = "search_web", Description = "Searches the web for information", Source = "Local" },
            new { Name = "read_document", Description = "Reads document content", Source = "System" },
        };

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = tools,
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("search_web", result);
        Assert.Contains("Searches the web for information", result);
        Assert.Contains("read_document", result);
        Assert.Contains("Reads document content", result);
        Assert.Contains("User tools", result);
        Assert.Contains("System tools", result);
    }

    [Fact]
    public async Task TaskPlanning_EmptyToolLists_RendersMinimalOutput()
    {
        var template = """
            {% assign hasUserTools = false %}{% assign hasSystemTools = false %}
            {% for tool in tools %}{% if tool.Source == "Local" %}{% assign hasUserTools = true %}{% endif %}{% if tool.Source == "System" %}{% assign hasSystemTools = true %}{% endif %}{% endfor %}
            {% if hasUserTools %}
            User tools available.
            {% endif %}
            {% if hasSystemTools %}
            System tools available.
            {% endif %}
            No tools needed.
            """;

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = Array.Empty<object>(),
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.DoesNotContain("User tools available", result);
        Assert.DoesNotContain("System tools available", result);
        Assert.Contains("No tools needed", result);
    }

    [Fact]
    public async Task DataSourceContextHeader_WithSearchToolName_RendersCorrectly()
    {
        var template = "Use the {{ searchToolName }} tool to search for relevant data sources.";

        var arguments = new Dictionary<string, object>
        {
            ["searchToolName"] = "search_data_source",
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Equal("Use the search_data_source tool to search for relevant data sources.", result);
    }

    [Fact]
    public async Task TabularBatchProcessing_WithBaseSystemMessage_RendersCorrectly()
    {
        var template = """
            {{ baseSystemMessage }}

            Process the data in tabular format.
            """;

        var arguments = new Dictionary<string, object>
        {
            ["baseSystemMessage"] = "You are a helpful data analyst.",
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("You are a helpful data analyst.", result);
        Assert.Contains("Process the data in tabular format.", result);
    }

    [Fact]
    public async Task ToolRegistryEntry_DescriptionConditional_HandlesNullDescription()
    {
        var template = """
            {% for tool in tools %}
            - {{ tool.Name }}{% if tool.Description %}: {{ tool.Description }}{% endif %}
            {% endfor %}
            """;

        var tools = new[]
        {
            new ToolRegistryEntry
            {
                Id = "tool1",
                Name = "tool_with_desc",
                Description = "Has a description",
                Source = ToolRegistryEntrySource.Local,
            },
            new ToolRegistryEntry
            {
                Id = "tool2",
                Name = "tool_no_desc",
                Description = null,
                Source = ToolRegistryEntrySource.Local,
            },
        };

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = tools,
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("tool_with_desc: Has a description", result);
        Assert.Contains("tool_no_desc", result);
        Assert.DoesNotContain("tool_no_desc:", result);
    }

    [Fact]
    public async Task ChatInteractionDocumentInfo_DefaultFilter_HandlesNullContentType()
    {
        var template = """
            {% for doc in docs %}
            - {{ doc.FileName }} ({{ doc.ContentType | default: "unknown" }})
            {% endfor %}
            """;

        var docs = new[]
        {
            new ChatInteractionDocumentInfo
            {
                DocumentId = "doc1",
                FileName = "file.txt",
                ContentType = null,
                FileSize = 100,
            },
        };

        var arguments = new Dictionary<string, object>
        {
            ["docs"] = docs,
        };

        var result = await _engine.RenderAsync(template, arguments);

        Assert.Contains("file.txt", result);
        Assert.Contains("unknown", result);
    }
}
