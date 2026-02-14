using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class OrchestrationContextTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var context = new OrchestrationContext();

        Assert.Null(context.UserMessage);
        Assert.Null(context.SourceName);
        Assert.Null(context.CompletionContext);
        Assert.Empty(context.ConversationHistory);
        Assert.Empty(context.Documents);
        Assert.Empty(context.Properties);
    }

    [Fact]
    public void Properties_AreCaseInsensitive()
    {
        var context = new OrchestrationContext();
        context.Properties["Key"] = "value";

        Assert.True(context.Properties.ContainsKey("key"));
        Assert.True(context.Properties.ContainsKey("KEY"));
        Assert.Equal("value", context.Properties["key"]);
    }

    [Fact]
    public void Documents_CanBePopulated()
    {
        var context = new OrchestrationContext();

        context.Documents.Add(new ChatInteractionDocumentInfo
        {
            DocumentId = "test",
            FileName = "test.pdf",
        });

        Assert.Single(context.Documents);
        Assert.Equal("test", context.Documents[0].DocumentId);
    }

    [Fact]
    public void Documents_CanBeReplaced()
    {
        var context = new OrchestrationContext();

        var docs = new List<ChatInteractionDocumentInfo>
        {
            new() { DocumentId = "a" },
            new() { DocumentId = "b" },
        };

        context.Documents = docs;

        Assert.Equal(2, context.Documents.Count);
        Assert.Same(docs, context.Documents);
    }
}
