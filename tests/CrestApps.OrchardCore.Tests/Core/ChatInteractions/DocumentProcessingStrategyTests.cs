using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Strategies;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.ChatInteractions;

public sealed class DocumentProcessingStrategyTests
{
    private static Mock<IChatInteractionDocumentStore> CreateMockDocumentStore(List<ChatInteractionDocument> documents)
    {
        var mockStore = new Mock<IChatInteractionDocumentStore>();
        mockStore.Setup(s => s.GetAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync((IEnumerable<string> ids) =>
                documents.Where(d => ids.Contains(d.ItemId)).ToList());
        mockStore.Setup(s => s.GetDocuments(It.IsAny<string>()))
            .ReturnsAsync(documents);
        return mockStore;
    }

    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (context, documents) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var mockStore = CreateMockDocumentStore(documents);
        var strategy = new SummarizationDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(context);
        Assert.True(context.Result.HasContext);
        Assert.True(context.Result.IsSuccess);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.DocumentQnA);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new SummarizationDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task SummarizationStrategy_ProcessAsync_ReturnsDocumentContent()
    {
        var (context, documents) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var mockStore = CreateMockDocumentStore(documents);
        var strategy = new SummarizationDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(context);

        Assert.True(context.Result.HasContext);
        Assert.True(context.Result.IsSuccess);
        Assert.Contains("Sample document content", context.Result.GetCombinedContext());
        Assert.False(context.Result.UsedVectorSearch);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (correctContext, correctDocuments) = CreateCsvProcessingContext();
        var mockStore = CreateMockDocumentStore(correctDocuments);
        var strategy = new TabularAnalysisDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);
        Assert.True(correctContext.Result.IsSuccess);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new TabularAnalysisDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task TabularAnalysisStrategy_ProcessAsync_ReturnsCsvContent()
    {
        var (context, documents) = CreateCsvProcessingContext();
        var mockStore = CreateMockDocumentStore(documents);
        var strategy = new TabularAnalysisDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(context);

        Assert.True(context.Result.HasContext);
        Assert.True(context.Result.IsSuccess);
        Assert.Contains("Name,Age,City", context.Result.GetCombinedContext());
        Assert.False(context.Result.UsedVectorSearch);
    }

    [Fact]
    public async Task ExtractionStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (correctContext, correctDocuments) = CreateProcessingContext(DocumentIntents.ExtractStructuredData);
        var mockStore = CreateMockDocumentStore(correctDocuments);
        var strategy = new ExtractionDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new ExtractionDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task ComparisonStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (correctContext, correctDocuments) = CreateProcessingContext(DocumentIntents.CompareDocuments);
        var mockStore = CreateMockDocumentStore(correctDocuments);
        var strategy = new ComparisonDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new ComparisonDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task TransformationStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (correctContext, correctDocuments) = CreateProcessingContext(DocumentIntents.TransformFormat);
        var mockStore = CreateMockDocumentStore(correctDocuments);
        var strategy = new TransformationDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new TransformationDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task GeneralReferenceStrategy_ProcessAsync_HandlesCorrectIntent()
    {
        var (correctContext, correctDocuments) = CreateProcessingContext(DocumentIntents.GeneralChatWithReference);
        var mockStore = CreateMockDocumentStore(correctDocuments);
        var strategy = new GeneralReferenceDocumentProcessingStrategy(mockStore.Object);

        await strategy.ProcessAsync(correctContext);
        Assert.True(correctContext.Result.HasContext);

        var (wrongContext, wrongDocuments) = CreateProcessingContext(DocumentIntents.SummarizeDocument);
        var wrongMockStore = CreateMockDocumentStore(wrongDocuments);
        var wrongStrategy = new GeneralReferenceDocumentProcessingStrategy(wrongMockStore.Object);
        await wrongStrategy.ProcessAsync(wrongContext);
        Assert.False(wrongContext.Result.HasContext);
    }

    [Fact]
    public async Task AllStrategies_ProcessAsync_AddContextForMatchingIntent()
    {
        var documents = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "document.txt",
                ContentType = "text/plain",
                Text = "Sample document content"
            }
        };
        var mockStore = CreateMockDocumentStore(documents);

        var csvDocuments = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "data.csv",
                ContentType = "text/csv",
                Text = "Name,Age,City\nJohn,30,NYC"
            }
        };
        var csvMockStore = CreateMockDocumentStore(csvDocuments);

        var intents = new[]
        {
            DocumentIntents.SummarizeDocument,
            DocumentIntents.AnalyzeTabularData,
            DocumentIntents.ExtractStructuredData,
            DocumentIntents.CompareDocuments,
            DocumentIntents.TransformFormat,
            DocumentIntents.GeneralChatWithReference,
        };

        var strategies = new IPromptProcessingStrategy[]
        {
            new SummarizationDocumentProcessingStrategy(mockStore.Object),
            new TabularAnalysisDocumentProcessingStrategy(csvMockStore.Object),
            new ExtractionDocumentProcessingStrategy(mockStore.Object),
            new ComparisonDocumentProcessingStrategy(mockStore.Object),
            new TransformationDocumentProcessingStrategy(mockStore.Object),
            new GeneralReferenceDocumentProcessingStrategy(mockStore.Object),
        };

        for (var i = 0; i < strategies.Length; i++)
        {
            var docsToUse = intents[i] == DocumentIntents.AnalyzeTabularData ? csvDocuments : documents;
            var docInfos = docsToUse.Select(d => new ChatInteractionDocumentInfo
            {
                DocumentId = d.ItemId,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.Text?.Length ?? 0
            }).ToList();

            var context = new IntentProcessingContext
            {
                Prompt = "Test prompt",
                Interaction = new ChatInteraction
                {
                    ItemId = "test-id",
                    Documents = docInfos
                },
            };
            context.Result.Intent = intents[i];

            await strategies[i].ProcessAsync(context);

            Assert.True(context.Result.HasContext, $"Strategy {strategies[i].GetType().Name} should add context for its intent");
            Assert.True(context.Result.IsSuccess, $"Strategy {strategies[i].GetType().Name} should have success status");
            Assert.NotEmpty(context.Result.AdditionalContexts);
        }
    }

    [Fact]
    public async Task Strategies_ProcessAsync_DoNotAddContextForWrongIntent()
    {
        var documents = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "document.txt",
                ContentType = "text/plain",
                Text = "Sample document content"
            }
        };
        var mockStore = CreateMockDocumentStore(documents);

        var strategies = new IPromptProcessingStrategy[]
        {
            new SummarizationDocumentProcessingStrategy(mockStore.Object),
            new TabularAnalysisDocumentProcessingStrategy(mockStore.Object),
            new ExtractionDocumentProcessingStrategy(mockStore.Object),
            new ComparisonDocumentProcessingStrategy(mockStore.Object),
            new TransformationDocumentProcessingStrategy(mockStore.Object),
            new GeneralReferenceDocumentProcessingStrategy(mockStore.Object),
        };

        // Use an intent that none of these strategies handle (DocumentQnA requires RAG strategy)
        var (context, _) = CreateProcessingContext(DocumentIntents.DocumentQnA);

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
        var documents = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "document.txt",
                ContentType = "text/plain",
                Text = "Sample document content"
            }
        };
        var mockStore = CreateMockDocumentStore(documents);

        // Create a context that could match multiple strategies
        var context = new IntentProcessingContext
        {
            Prompt = "Summarize and compare",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "document.txt",
                        ContentType = "text/plain",
                        FileSize = 100
                    }
                ]
            },
        };
        context.Result.Intent = DocumentIntents.SummarizeDocument;

        // First strategy adds context
        var summarizationStrategy = new SummarizationDocumentProcessingStrategy(mockStore.Object);
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

    private static (IntentProcessingContext context, List<ChatInteractionDocument> documents) CreateProcessingContext(string intent)
    {
        var documents = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "document.txt",
                ContentType = "text/plain",
                Text = "Sample document content"
            }
        };

        var ctx = new IntentProcessingContext
        {
            Prompt = "Test prompt",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "document.txt",
                        ContentType = "text/plain",
                        FileSize = 100
                    }
                ]
            },
        };

        ctx.Result.Intent = intent;

        return (ctx, documents);
    }

    private static (IntentProcessingContext context, List<ChatInteractionDocument> documents) CreateCsvProcessingContext()
    {
        var documents = new List<ChatInteractionDocument>
        {
            new()
            {
                ItemId = "doc1",
                FileName = "data.csv",
                ContentType = "text/csv",
                Text = "Name,Age,City\nJohn,30,NYC\nJane,25,LA"
            }
        };

        var ctx = new IntentProcessingContext
        {
            Prompt = "Analyze this data",
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "data.csv",
                        ContentType = "text/csv",
                        FileSize = 100
                    }
                ]
            },
        };

        ctx.Result.Intent = DocumentIntents.AnalyzeTabularData;

        return (ctx, documents);
    }

    [Fact]
    public void ImageGeneration_BuildPromptWithHistory_FallsBackToPrompt_WhenNoHistory()
    {
        var method = typeof(ImageGenerationDocumentProcessingStrategy)
            .GetMethod("BuildPromptWithHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var enhanced = (string)method!.Invoke(null, ["Create an image with blue skies", null, 5]);

        Assert.Equal("Create an image with blue skies", enhanced);
        Assert.DoesNotContain("Conversation context", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void ImageGeneration_BuildPromptWithHistory_IncludesHistoryAndCurrentRequest()
    {
        var method = typeof(ImageGenerationDocumentProcessingStrategy)
            .GetMethod("BuildPromptWithHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Analyze this data"),
            new(ChatRole.Assistant, "Here is the extracted table: A,B,C"),
        };

        var enhanced = (string)method!.Invoke(null, ["Use that data to create an image chart", history, 5]);

        Assert.Contains("Conversation context", enhanced, StringComparison.Ordinal);
        Assert.Contains("user:", enhanced, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analyze this data", enhanced, StringComparison.Ordinal);
        Assert.Contains("assistant:", enhanced, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extracted table", enhanced, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Current request:", enhanced, StringComparison.Ordinal);
        Assert.Contains("Use that data to create an image chart", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartGeneration_BuildPromptWithHistory_FallsBackToPrompt_WhenNoHistory()
    {
        var method = typeof(ChartGenerationDocumentProcessingStrategy)
            .GetMethod("BuildPromptWithHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var enhanced = (string)method!.Invoke(null, ["Create a bar chart", null, 10]);

        Assert.Equal("Create a bar chart", enhanced);
        Assert.DoesNotContain("Conversation context", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartGeneration_BuildPromptWithHistory_IncludesHistoryAndCurrentRequest()
    {
        var method = typeof(ChartGenerationDocumentProcessingStrategy)
            .GetMethod("BuildPromptWithHistory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "Sales by month: Jan=10, Feb=20, Mar=30"),
            new(ChatRole.Assistant, "Noted. You can plot these values."),
        };

        var enhanced = (string)method!.Invoke(null, ["Create a bar chart", history, 10]);

        Assert.Contains("Conversation context with data to visualize", enhanced, StringComparison.Ordinal);
        Assert.Contains("User:", enhanced, StringComparison.Ordinal);
        Assert.Contains("Sales by month", enhanced, StringComparison.Ordinal);
        Assert.Contains("Assistant:", enhanced, StringComparison.Ordinal);
        Assert.Contains("Current request:", enhanced, StringComparison.Ordinal);
        Assert.Contains("Create a bar chart", enhanced, StringComparison.Ordinal);
    }

    [Fact]
    public void ChartGeneration_ExtractJsonFromResponse_ExtractsFromMarkdownJsonBlock()
    {
        var method = typeof(ChartGenerationDocumentProcessingStrategy)
            .GetMethod("ExtractJsonFromResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var response = "```json\n{\"type\":\"bar\",\"data\":{}}\n```";
        var json = (string)method!.Invoke(null, [response]);

        Assert.Equal("{\"type\":\"bar\",\"data\":{}}", json);
    }

    [Fact]
    public void ChartGeneration_ExtractJsonFromResponse_ExtractsJsonObject_WhenSurroundedByText()
    {
        var method = typeof(ChartGenerationDocumentProcessingStrategy)
            .GetMethod("ExtractJsonFromResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var response = "Here is the config: {\"type\":\"pie\",\"data\":{}} Thanks!";
        var json = (string)method!.Invoke(null, [response]);

        Assert.Equal("{\"type\":\"pie\",\"data\":{}}", json);
    }

    [Fact]
    public void ChartGeneration_ExtractJsonFromResponse_ReturnsNull_WhenNoJson()
    {
        var method = typeof(ChartGenerationDocumentProcessingStrategy)
            .GetMethod("ExtractJsonFromResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        var json = (string)method!.Invoke(null, ["No json here"]);

        Assert.Null(json);
    }
}
