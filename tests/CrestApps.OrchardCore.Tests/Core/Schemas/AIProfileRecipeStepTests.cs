using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class AIProfileRecipeStepTests
{
    [Fact]
    public async Task GetSchemaAsync_ContainsCurrentProfileFields()
    {
        var step = new AIProfileRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"Agent\"", json);
        Assert.Contains("\"Description\"", json);
        Assert.Contains("\"PromptSubject\"", json);
        Assert.Contains("\"OrchestratorName\"", json);
        Assert.Contains("\"ChatDeploymentName\"", json);
        Assert.Contains("\"UtilityDeploymentName\"", json);
        Assert.Contains("\"CreatedUtc\"", json);
        Assert.Contains("\"OwnerId\"", json);
        Assert.Contains("\"Author\"", json);
        Assert.Contains("\"AIProfileMetadata\"", json);
        Assert.Contains("\"FunctionInvocationMetadata\"", json);
        Assert.Contains("\"PromptTemplateMetadata\"", json);
        Assert.Contains("\"AIProfileDataExtractionSettings\"", json);
        Assert.Contains("\"AIProfilePostSessionSettings\"", json);
        Assert.Contains("\"ChatModeProfileSettings\"", json);
        Assert.Contains("\"ClaudeSessionMetadata\"", json);
        Assert.Contains("\"CopilotSessionMetadata\"", json);
        Assert.DoesNotContain("\"ChatDeploymentId\"", json);
        Assert.DoesNotContain("\"UtilityDeploymentId\"", json);
    }

    [Fact]
    public async Task CreateFromTemplateSchema_ContainsRequiredTemplateId()
    {
        var step = new CreateAIProfileFromTemplateRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"CreateAIProfileFromTemplate\"", json);
        Assert.Contains("\"TemplateId\"", json);
        Assert.Contains("\"Source\"", json);
        Assert.Contains("\"DisplayText\"", json);
        Assert.Contains("\"Agent\"", json);
        Assert.Contains("\"Description\"", json);
        Assert.Contains("\"PromptSubject\"", json);
        Assert.Contains("\"OrchestratorName\"", json);
        Assert.Contains("\"Properties\"", json);
        Assert.Contains("\"Settings\"", json);
        Assert.Contains("\"AIProfileMetadata\"", json);
        Assert.Contains("\"AIProfileDataExtractionSettings\"", json);
        Assert.Contains("\"ClaudeSessionMetadata\"", json);
        Assert.Contains("\"CopilotSessionMetadata\"", json);
        Assert.DoesNotContain("\"ChatDeploymentId\"", json);
        Assert.DoesNotContain("\"UtilityDeploymentId\"", json);
    }

    [Fact]
    public async Task AIProfileTemplateSchema_ContainsSharedAndTemplateSpecificKnownProperties()
    {
        var step = new AIProfileTemplateRecipeStep(Options.Create(new AIOptions()));
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        var json = JsonSerializer.Serialize(schema);
        var schemaNode = JsonNode.Parse(json)!;
        var requiredValues = schemaNode["properties"]?["Templates"]?["items"]?["required"]?
            .AsArray()
            .Select(node => node?.GetValue<string>())
            .ToArray();

        Assert.Contains("\"AIProfileTemplate\"", json);
        Assert.Contains("\"Source\"", json);
        Assert.Contains("\"Properties\"", json);
        Assert.Contains("\"AgentMetadata\"", json);
        Assert.Contains("\"AIProfileMetadata\"", json);
        Assert.Contains("\"FunctionInvocationMetadata\"", json);
        Assert.Contains("\"AgentInvocationMetadata\"", json);
        Assert.Contains("\"PromptTemplateMetadata\"", json);
        Assert.Contains("\"AnalyticsMetadata\"", json);
        Assert.Contains("\"DataSourceMetadata\"", json);
        Assert.Contains("\"AIDataSourceRagMetadata\"", json);
        Assert.Contains("\"DocumentsMetadata\"", json);
        Assert.Contains("\"MemoryMetadata\"", json);
        Assert.Contains("\"ClaudeSessionMetadata\"", json);
        Assert.Contains("\"CopilotSessionMetadata\"", json);
        Assert.Contains("\"AIProfileMcpMetadata\"", json);
        Assert.Contains("\"AIProfileA2AMetadata\"", json);
        Assert.Contains("\"ProfileTemplateMetadata\"", json);
        Assert.Contains("\"SystemPromptTemplateMetadata\"", json);
        Assert.Contains("\"AIChatProfileSettings\"", json);
        Assert.Contains("\"AIProfileDataExtractionSettings\"", json);
        Assert.Contains("\"AIProfilePostSessionSettings\"", json);
        Assert.Contains("\"ChatModeProfileSettings\"", json);
        Assert.Contains("\"InitialResponseHandlerName\"", json);
        Assert.Contains("\"AgentAvailability\"", json);
        Assert.Equal(["Name", "Source", "DisplayText"], requiredValues);
    }

    [Fact]
    public async Task McpAndA2ARecipeSchemas_ContainKnownMetadataObjects()
    {
        var steps = new IRecipeStep[]
        {
            new McpConnectionRecipeStep(),
            new McpResourceRecipeStep(),
            new McpPromptRecipeStep(),
            new A2AConnectionRecipeStep(),
            new AIDataSourceRecipeStep(CreateAIDataSourceSourceOptions()),
        };

        var json = JsonSerializer.Serialize(await Task.WhenAll(steps.Select(step => step.GetSchemaAsync(TestContext.Current.CancellationToken).AsTask())));

        Assert.Contains("\"SseMcpConnectionMetadata\"", json);
        Assert.Contains("\"StdioMcpConnectionMetadata\"", json);
        Assert.Contains("\"FtpConnectionMetadata\"", json);
        Assert.Contains("\"SftpConnectionMetadata\"", json);
        Assert.Contains("\"A2AConnectionMetadata\"", json);
        Assert.Contains("\"AIDataSource\"", json);
        Assert.Contains("\"McpConnection\"", json);
        Assert.Contains("\"McpPrompt\"", json);
        Assert.Contains("\"McpResource\"", json);
    }

    [Fact]
    public async Task AIDataSourceSchema_UsesRegisteredSourceTypesForSourceEnums()
    {
        var step = new AIDataSourceRecipeStep(CreateAIDataSourceSourceOptions());
        var json = JsonNode.Parse(JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken)))!;
        var sourceValues = json["properties"]?["DataSources"]?["items"]?["properties"]?["Source"]?["enum"]?
            .AsArray()
            .Select(node => node?.GetValue<string>())
            .ToArray();

        Assert.Equal(
            [
                "AzureAISearch",
                "Elasticsearch",
                "PostgreSQL",
                "SearchIndexProfile",
            ],
            sourceValues);
    }

    [Fact]
    public async Task AIProviderConnectionsSchema_UsesRegisteredProviderNamesForSourceEnums()
    {
        var options = new AIOptions();
        var connectionSources = Assert.IsAssignableFrom<IDictionary<string, AIProviderConnectionOptionsEntry>>(options.ConnectionSources);
        connectionSources["OpenAI"] = new AIProviderConnectionOptionsEntry("OpenAI");
        connectionSources["Azure"] = new AIProviderConnectionOptionsEntry("Azure");
        connectionSources["openai"] = new AIProviderConnectionOptionsEntry("OpenAI");
        var step = new AIProviderConnectionsRecipeStep(Microsoft.Extensions.Options.Options.Create(options));

        var json = JsonNode.Parse(JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken)))!;
        var sourceValues = json["properties"]?["Connections"]?["items"]?["properties"]?["Source"]?["enum"]?
            .AsArray()
            .Select(node => node?.GetValue<string>())
            .ToArray();
        var clientNameValues = json["properties"]?["Connections"]?["items"]?["properties"]?["ClientName"]?["enum"]?
            .AsArray()
            .Select(node => node?.GetValue<string>())
            .ToArray();

        Assert.Equal(["Azure", "OpenAI"], sourceValues);
        Assert.Equal(["Azure", "OpenAI"], clientNameValues);
    }

    private static IOptions<AIDataSourceSourceOptions> CreateAIDataSourceSourceOptions()
    {
        var options = new AIDataSourceSourceOptions();
        options.AddOrUpdate("SearchIndexProfile", new("Search Index Profile", "Search Index Profile"), new("Read source documents from an Orchard-managed search index profile.", "Read source documents from an Orchard-managed search index profile."));
        options.AddOrUpdate("AzureAISearch", new("Azure AI Search", "Azure AI Search"), new("Read source documents from an external Azure AI Search index.", "Read source documents from an external Azure AI Search index."));
        options.AddOrUpdate("Elasticsearch", new("Elasticsearch", "Elasticsearch"), new("Read source documents from an external Elasticsearch index.", "Read source documents from an external Elasticsearch index."));
        options.AddOrUpdate("PostgreSQL", new("PostgreSQL", "PostgreSQL"), new("Read source documents from a PostgreSQL table.", "Read source documents from a PostgreSQL table."));

        return Options.Create(options);
    }
}
