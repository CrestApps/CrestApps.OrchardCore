using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.ChatInteractions;

public sealed class DocumentProcessingStrategyTests
{
    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new SummarizationDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);
        Assert.True(correctContext.Result.IsSuccess);

        var wrongContext = CreateProcessingContext(DocumentIntents.DocumentQnA);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_ReturnsDocumentContent()
    {
        var strategy = new SummarizationDocumentProcessingStrategy();
        var context = CreateProcessingContext(DocumentIntents.SummarizeDocument);

        await strategy.ProcessAsync(context);

        Assert.True(context.Result.HasContext);
        Assert.True(context.Result.IsSuccess);
        Assert.Contains("Sample document content", context.Result.GetCombinedContext());
        Assert.False(context.Result.UsedVectorSearch);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();

        var correctContext = CreateCsvProcessingContext();
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);
        Assert.True(correctContext.Result.IsSuccess);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_ReturnsCsvContent()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();
        var context = CreateCsvProcessingContext();

        await strategy.ProcessAsync(context);

        Assert.True(context.Result.HasContext);
        Assert.True(context.Result.IsSuccess);
        Assert.Contains("Name,Age,City", context.Result.GetCombinedContext());
        Assert.False(context.Result.UsedVectorSearch);
    }

    [Fact]
    public async Task ExtractionStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new ExtractionDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.ExtractStructuredData);
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task ComparisonStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new ComparisonDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.CompareDocuments);
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task TransformationStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new TransformationDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.TransformFormat);
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task GeneralReferenceStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new GeneralReferenceDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.GeneralChatWithReference);
        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task AllStrategies_ProcessAsync_AddContextForMatchingIntent()
    {
        var strategies = new IDocumentProcessingStrategy[]
        {
            new SummarizationDocumentProcessingStrategy(),
            new TabularAnalysisDocumentProcessingStrategy(),
            new ExtractionDocumentProcessingStrategy(),
            new ComparisonDocumentProcessingStrategy(),
            new TransformationDocumentProcessingStrategy(),
            new GeneralReferenceDocumentProcessingStrategy(),
        };

        var intents = new[]
        {
            DocumentIntents.SummarizeDocument,
            DocumentIntents.AnalyzeTabularData,
            DocumentIntents.ExtractStructuredData,
            DocumentIntents.CompareDocuments,
            DocumentIntents.TransformFormat,
            DocumentIntents.GeneralChatWithReference,
        };

        for (var i = 0; i < strategies.Length; i++)
        {
            var context = CreateProcessingContext(intents[i]);
            await strategies[i].ProcessAsync(context);

            Assert.True(context.Result.HasContext, $"Strategy {strategies[i].GetType().Name} should add context for its intent");
            Assert.True(context.Result.IsSuccess, $"Strategy {strategies[i].GetType().Name} should have success status");
            Assert.NotEmpty(context.Result.AdditionalContexts);
        }
    }

    [Fact]
    public async Task Strategies_ProcessAsync_DoNotAddContextForWrongIntent()
    {
        var strategies = new IDocumentProcessingStrategy[]
        {
            new SummarizationDocumentProcessingStrategy(),
            new TabularAnalysisDocumentProcessingStrategy(),
            new ExtractionDocumentProcessingStrategy(),
            new ComparisonDocumentProcessingStrategy(),
            new TransformationDocumentProcessingStrategy(),
            new GeneralReferenceDocumentProcessingStrategy(),
        };

        // Use an intent that none of these strategies handle (DocumentQnA requires RAG strategy)
        var context = CreateProcessingContext(DocumentIntents.DocumentQnA);

        foreach (var strategy in strategies)
        {
            await strategy.ProcessAsync(context);
        }

        // Since we use the same context, it should still have no content after all strategies run
        Assert.False(context.Result.HasContext, "No strategy should add context for DocumentQnA intent");
    }

    [Fact]
    public async Task MultipleStrategies_ProcessAsync_CanAccumulateContext()
    {
        // Create a context that could match multiple strategies
        var context = new IntentProcessingContext
        {
            Prompt = "Summarize and compare",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocument
                    {
                        DocumentId = "doc1",
                        FileName = "document.txt",
                        ContentType = "text/plain",
                        Text = "Sample document content"
                    }
                ]
            },
        };
        context.Result.Intent = DocumentIntents.SummarizeDocument;

        // First strategy adds context
        var summarizationStrategy = new SummarizationDocumentProcessingStrategy();
        await summarizationStrategy.ProcessAsync(context);
        Assert.Single(context.Result.AdditionalContexts);

        // Manually add more context (simulating multiple strategies contributing)
        context.Result.AddContext("Additional comparison context", "Comparison prefix:");
        Assert.Equal(2, context.Result.AdditionalContexts.Count);

        // Combined context should include both
        var combined = context.Result.GetCombinedContext();
        Assert.Contains("Sample document content", combined);
        Assert.Contains("Additional comparison context", combined);
    }

    [Fact]
    public void DocumentProcessingResult_AddContext_AccumulatesMultipleContexts()
    {
        var result = new IntentProcessingResult();

        Assert.False(result.HasContext);

        result.AddContext("First context", "Prefix 1:");
        Assert.True(result.HasContext);
        Assert.Single(result.AdditionalContexts);

        result.AddContext("Second context", "Prefix 2:");
        Assert.Equal(2, result.AdditionalContexts.Count);

        var combined = result.GetCombinedContext();
        Assert.Contains("First context", combined);
        Assert.Contains("Second context", combined);
        Assert.Contains("---", combined); // Separator between contexts
    }

    [Fact]
    public void DocumentProcessingResult_AddContext_TracksVectorSearch()
    {
        var result = new IntentProcessingResult();

        Assert.False(result.UsedVectorSearch);

        result.AddContext("Context without vector search", usedVectorSearch: false);
        Assert.False(result.UsedVectorSearch);

        result.AddContext("Context with vector search", usedVectorSearch: true);
        Assert.True(result.UsedVectorSearch);
    }

    private static IntentProcessingContext CreateProcessingContext(string intent)
    {
        var ctx = new IntentProcessingContext
        {
            Prompt = "Test prompt",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocument
                    {
                        DocumentId = "doc1",
                        FileName = "document.txt",
                        ContentType = "text/plain",
                        Text = "Sample document content"
                    }
                ]
            },
        };

        ctx.Result.Intent = intent;

        return ctx;
    }

    private static IntentProcessingContext CreateCsvProcessingContext()
    {
        var ctx = new IntentProcessingContext
        {
            Prompt = "Analyze this data",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocument
                    {
                        DocumentId = "doc1",
                        FileName = "data.csv",
                        ContentType = "text/csv",
                        Text = "Name,Age,City\nJohn,30,NYC\nJane,25,LA"
                    }
                ]
            },
        };

        ctx.Result.Intent = DocumentIntents.AnalyzeTabularData;

        return ctx;
    }
}
