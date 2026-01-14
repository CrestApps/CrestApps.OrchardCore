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
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);
        Assert.True(correctResult.IsSuccess);

        var wrongContext = CreateProcessingContext(DocumentIntents.DocumentQnA);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_ReturnsDocumentContent()
    {
        var strategy = new SummarizationDocumentProcessingStrategy();
        var context = CreateProcessingContext(DocumentIntents.SummarizeDocument);

        var result = await strategy.ProcessAsync(context);

        Assert.True(result.Handled);
        Assert.True(result.IsSuccess);
        Assert.Contains("Sample document content", result.AdditionalContext);
        Assert.False(result.UsedVectorSearch);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();

        var correctContext = CreateCsvProcessingContext();
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);
        Assert.True(correctResult.IsSuccess);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_ReturnsCsvContent()
    {
        var strategy = new TabularAnalysisDocumentProcessingStrategy();
        var context = CreateCsvProcessingContext();

        var result = await strategy.ProcessAsync(context);

        Assert.True(result.Handled);
        Assert.True(result.IsSuccess);
        Assert.Contains("Name,Age,City", result.AdditionalContext);
        Assert.False(result.UsedVectorSearch);
    }

    [Fact]
    public async Task ExtractionStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new ExtractionDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.ExtractStructuredData);
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task ComparisonStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new ComparisonDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.CompareDocuments);
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task TransformationStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new TransformationDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.TransformFormat);
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task GeneralReferenceStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var strategy = new GeneralReferenceDocumentProcessingStrategy();

        var correctContext = CreateProcessingContext(DocumentIntents.GeneralChatWithReference);
        var correctResult = await strategy.ProcessAsync(correctContext);
        Assert.True(correctResult.Handled);

        var wrongContext = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongResult = await strategy.ProcessAsync(wrongContext);
        Assert.False(wrongResult.Handled);
    }

    [Fact]
    public async Task AllStrategies_ProcessAsync_ReturnHandledWithContent()
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

            Assert.True(result.Handled, $"Strategy {strategies[i].GetType().Name} should handle its intent");
            Assert.True(result.IsSuccess, $"Strategy {strategies[i].GetType().Name} should return success");
            Assert.NotNull(result.AdditionalContext);
        }
    }

    [Fact]
    public async Task Strategies_ProcessAsync_ReturnNotHandledForWrongIntent()
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
            var result = await strategy.ProcessAsync(context);
            Assert.False(result.Handled, $"Strategy {strategy.GetType().Name} should not handle DocumentQnA intent");
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
