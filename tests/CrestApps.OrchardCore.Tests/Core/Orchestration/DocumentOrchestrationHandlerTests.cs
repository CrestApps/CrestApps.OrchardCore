using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DocumentOrchestrationHandlerTests
{
    private static DocumentOrchestrationHandler CreateHandler(AIToolDefinitionOptions toolOptions = null)
    {
        toolOptions ??= new AIToolDefinitionOptions();

        return new DocumentOrchestrationHandler(
            Options.Create(toolOptions),
            new FakeAITemplateService());
    }

    private static AIToolDefinitionOptions CreateToolOptionsWithDocTools()
    {
        var options = new AIToolDefinitionOptions();
        options.SetTool("read_document", new AIToolDefinitionEntry(typeof(object))
        {
            Name = "read_document",
            Description = "Reads document content",
            Purpose = AIToolPurposes.DocumentProcessing,
        });

        return options;
    }

    [Fact]
    public async Task BuildingAsync_ChatInteractionWithDocuments_PopulatesContext()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction
        {
            ItemId = "interaction1",
            Documents =
            [
                new ChatInteractionDocumentInfo
                {
                    DocumentId = "doc1",
                    FileName = "report.pdf",
                    ContentType = "application/pdf",
                    FileSize = 1024,
                },
            ],
        };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Single(context.Documents);
        Assert.Equal("doc1", context.Documents[0].DocumentId);
        Assert.Equal("report.pdf", context.Documents[0].FileName);
    }

    [Fact]
    public async Task BuildingAsync_ChatInteractionWithNoDocuments_LeavesEmpty()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction { Documents = [] };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_ChatInteractionWithNullDocuments_LeavesEmpty()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction { Documents = null };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_NonChatInteractionResource_LeavesEmpty()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext();

        var profile = new AIProfile { DisplayText = "Test Profile" };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(profile, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_MultipleDocuments_AllPopulated()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction
        {
            Documents =
            [
                new ChatInteractionDocumentInfo { DocumentId = "doc1", FileName = "file1.pdf" },
                new ChatInteractionDocumentInfo { DocumentId = "doc2", FileName = "file2.csv" },
                new ChatInteractionDocumentInfo { DocumentId = "doc3", FileName = "file3.xlsx" },
            ],
        };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Equal(3, context.Documents.Count);
    }

    [Fact]
    public async Task BuiltAsync_WithDocuments_EnrichesSystemMessage()
    {
        var handler = CreateHandler(CreateToolOptionsWithDocTools());
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
            Documents =
            [
                new ChatInteractionDocumentInfo
                {
                    DocumentId = "doc1",
                    FileName = "report.pdf",
                    ContentType = "application/pdf",
                    FileSize = 2048,
                },
            ],
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new ChatInteraction(), context));

        var systemMessage = context.SystemMessageBuilder.ToString();
        Assert.Contains("report.pdf", systemMessage);
        Assert.Contains("read_document", systemMessage);
        // chat_interaction_id is NOT in the system message — it is resolved server-side.
        Assert.DoesNotContain("chat_interaction_id", systemMessage);
    }

    [Fact]
    public async Task BuiltAsync_WithoutDocuments_NoChanges()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.Null(context.CompletionContext.SystemMessage);
    }

    [Fact]
    public async Task BuiltAsync_WithDocuments_DoesNotModifyToolNames()
    {
        var handler = CreateHandler(CreateToolOptionsWithDocTools());
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext
            {
                ToolNames = ["existing_tool"],
            },
            Documents =
            [
                new ChatInteractionDocumentInfo
                {
                    DocumentId = "doc1",
                    FileName = "data.csv",
                    ContentType = "text/csv",
                    FileSize = 512,
                },
            ],
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new ChatInteraction(), context));

        // Document tools are system tools — the orchestrator always includes them.
        // The handler should NOT inject tool names.
        Assert.Single(context.CompletionContext.ToolNames);
        Assert.Contains("existing_tool", context.CompletionContext.ToolNames);
    }

    [Fact]
    public async Task BuiltAsync_WithDocuments_SetsHasDocumentsFlag()
    {
        var handler = CreateHandler(CreateToolOptionsWithDocTools());
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
            Documents =
            [
                new ChatInteractionDocumentInfo
                {
                    DocumentId = "doc1",
                    FileName = "report.pdf",
                    ContentType = "application/pdf",
                    FileSize = 1024,
                },
            ],
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new ChatInteraction(), context));

        Assert.True(context.CompletionContext.AdditionalProperties.ContainsKey(AICompletionContextKeys.HasDocuments));
        Assert.Equal(true, context.CompletionContext.AdditionalProperties[AICompletionContextKeys.HasDocuments]);
    }

    [Fact]
    public async Task BuiltAsync_WithoutDocuments_DoesNotSetHasDocumentsFlag()
    {
        var handler = CreateHandler();
        var context = new OrchestrationContext
        {
            CompletionContext = new AICompletionContext(),
        };

        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));

        Assert.False(context.CompletionContext.AdditionalProperties.ContainsKey(AICompletionContextKeys.HasDocuments));
    }

    private sealed class FakeAITemplateService : IAITemplateService
    {
        public Task<IReadOnlyList<AITemplate>> ListAsync()
            => Task.FromResult<IReadOnlyList<AITemplate>>([]);

        public Task<AITemplate> GetAsync(string id)
            => Task.FromResult<AITemplate>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
        {
            if (arguments != null && arguments.TryGetValue("tools", out var toolsObj) && toolsObj is IEnumerable<object> tools)
            {
                var lines = new List<string>
                {
                    "[Available Documents or attachments]",
                    "The user has uploaded the following documents as supplementary context.",
                    "",
                    "Available document tools:",
                };

                foreach (dynamic tool in tools)
                {
                    lines.Add($"- {tool.Name}: {tool.Description}");
                }

                if (arguments.TryGetValue("availableDocuments", out var docsObj) && docsObj is IEnumerable<object> docs)
                {
                    lines.Add("");
                    lines.Add("Available documents:");

                    foreach (dynamic doc in docs)
                    {
                        lines.Add($"- {doc.FileName} ({doc.ContentType}, {doc.FileSize} bytes)");
                    }
                }

                return Task.FromResult(string.Join(Environment.NewLine, lines));
            }

            return Task.FromResult($"[Template: {id}]");
        }

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult(string.Join(separator, ids.Select(id => $"[Template: {id}]")));
    }
}
