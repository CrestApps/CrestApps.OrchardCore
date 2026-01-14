using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.ChatInteractions;

public sealed class DocumentProcessingStrategyTests
{
    [Fact]
    public void SummarizationStrategy_CanHandle_ReturnsTrueForSummarizeDocument()
    {
        var strategy = new SummarizationDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.SummarizeDocument));
        Assert.False(strategy.CanHandle(DocumentIntents.DocumentQnA));
        Assert.False(strategy.CanHandle(DocumentIntents.AnalyzeTabularData));
    }

    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_ReturnsDocumentContent()
    {
        var strategy = new SummarizationDocumentProcessingStrategy();
        var context = CreateProcessingContext(DocumentIntents.SummarizeDocument);

        var result = await strategy.ProcessAsync(context);

        Assert.True(result.IsSuccess);
        Assert.Contains("Sample document content", result.AdditionalContext);
        Assert.False(result.UsedVectorSearch);
    }

    [Fact]
    public void TabularAnalysisStrategy_CanHandle_ReturnsTrueForAnalyzeTabularData()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.AnalyzeTabularData));
        Assert.False(strategy.CanHandle(DocumentIntents.SummarizeDocument));
        Assert.False(strategy.CanHandle(DocumentIntents.DocumentQnA));
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_ReturnsCsvContent()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();
        var context = CreateCsvProcessingContext();

        var result = await strategy.ProcessAsync(context);

        Assert.True(result.IsSuccess);
        Assert.Contains("Name,Age,City", result.AdditionalContext);
        Assert.False(result.UsedVectorSearch);
    }

    [Fact]
    public void ExtractionStrategy_CanHandle_ReturnsTrueForExtractStructuredData()
    {
        var strategy = new ExtractionDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.ExtractStructuredData));
        Assert.False(strategy.CanHandle(DocumentIntents.SummarizeDocument));
    }

    [Fact]
    public void ComparisonStrategy_CanHandle_ReturnsTrueForCompareDocuments()
    {
        var strategy = new ComparisonDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.CompareDocuments));
        Assert.False(strategy.CanHandle(DocumentIntents.SummarizeDocument));
    }

    [Fact]
    public void TransformationStrategy_CanHandle_ReturnsTrueForTransformFormat()
    {
        var strategy = new TransformationDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.TransformFormat));
        Assert.False(strategy.CanHandle(DocumentIntents.SummarizeDocument));
    }

    [Fact]
    public void GeneralReferenceStrategy_CanHandle_ReturnsTrueForGeneralChatWithReference()
    {
        var strategy = new GeneralReferenceDocumentProcessingStrategy();

        Assert.True(strategy.CanHandle(DocumentIntents.GeneralChatWithReference));
        Assert.False(strategy.CanHandle(DocumentIntents.SummarizeDocument));
    }

    [Fact]
    public async Task AllStrategies_ProcessAsync_ReturnSuccessWithContent()
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
            var result = await strategies[i].ProcessAsync(context);

            Assert.True(result.IsSuccess, $"Strategy {strategies[i].GetType().Name} should return success");
            Assert.NotNull(result.AdditionalContext);
        }
    }

    private static DocumentProcessingContext CreateProcessingContext(string intent)
    {
        return new DocumentProcessingContext
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
            IntentResult = DocumentIntentResult.FromIntent(intent)
        };
    }

    private static DocumentProcessingContext CreateCsvProcessingContext()
    {
        return new DocumentProcessingContext
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
            IntentResult = DocumentIntentResult.FromIntent(DocumentIntents.AnalyzeTabularData)
        };
    }
}
