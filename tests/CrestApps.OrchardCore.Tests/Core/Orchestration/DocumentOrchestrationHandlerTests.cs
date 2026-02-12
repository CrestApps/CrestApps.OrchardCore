using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DocumentOrchestrationHandlerTests
{
    [Fact]
    public async Task BuildingAsync_ChatInteractionWithDocuments_PopulatesContext()
    {
        var handler = new DocumentOrchestrationHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction
        {
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
        var handler = new DocumentOrchestrationHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction { Documents = [] };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_ChatInteractionWithNullDocuments_LeavesEmpty()
    {
        var handler = new DocumentOrchestrationHandler();
        var context = new OrchestrationContext();

        var interaction = new ChatInteraction { Documents = null };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(interaction, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_NonChatInteractionResource_LeavesEmpty()
    {
        var handler = new DocumentOrchestrationHandler();
        var context = new OrchestrationContext();

        var profile = new AIProfile { DisplayText = "Test Profile" };

        await handler.BuildingAsync(new OrchestrationContextBuildingContext(profile, context));

        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildingAsync_MultipleDocuments_AllPopulated()
    {
        var handler = new DocumentOrchestrationHandler();
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
    public async Task BuiltAsync_IsNoOp()
    {
        var handler = new DocumentOrchestrationHandler();
        var context = new OrchestrationContext();

        // Should not throw.
        await handler.BuiltAsync(new OrchestrationContextBuiltContext(new AIProfile(), context));
    }
}
