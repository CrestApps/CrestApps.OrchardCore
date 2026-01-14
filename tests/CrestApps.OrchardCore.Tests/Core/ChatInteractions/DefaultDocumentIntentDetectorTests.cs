using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Services;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.ChatInteractions;

public sealed class KeywordDocumentIntentDetectorTests
{
    private readonly KeywordDocumentIntentDetector _detector = new();

    [Fact]
    public async Task DetectAsync_WhenPromptIsEmpty_ReturnsGeneralChatWithReference()
    {
        var context = CreateContext("");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GeneralChatWithReference, result.Intent);
    }

    [Theory]
    [InlineData("summarize this document")]
    [InlineData("give me a summary of the file")]
    [InlineData("what are the key points?")]
    [InlineData("brief overview please")]
    [InlineData("tldr")]
    public async Task DetectAsync_WhenSummarizationKeywordsPresent_ReturnsSummarizeDocument(string prompt)
    {
        var context = CreateContext(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.SummarizeDocument, result.Intent);
    }

    [Theory]
    [InlineData("calculate the total sales")]
    [InlineData("what is the average revenue?")]
    [InlineData("show me the statistics")]
    [InlineData("calculate the sum")]
    public async Task DetectAsync_WhenTabularAnalysisKeywordsWithCsvFile_ReturnsAnalyzeTabularData(string prompt)
    {
        var context = CreateContextWithCsvDocument(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.AnalyzeTabularData, result.Intent);
    }

    [Theory]
    [InlineData("extract all the names")]
    [InlineData("get all email addresses")]
    [InlineData("parse the document for dates")]
    [InlineData("find all phone numbers")]
    public async Task DetectAsync_WhenExtractionKeywordsPresent_ReturnsExtractStructuredData(string prompt)
    {
        var context = CreateContext(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.ExtractStructuredData, result.Intent);
    }

    [Theory]
    [InlineData("compare these two documents")]
    [InlineData("what is the difference between them?")]
    [InlineData("how are they similar?")]
    public async Task DetectAsync_WhenComparisonKeywordsWithMultipleDocs_ReturnsCompareDocuments(string prompt)
    {
        var context = CreateContextWithMultipleDocuments(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.CompareDocuments, result.Intent);
    }

    [Theory]
    [InlineData("transform this content into a table")]
    [InlineData("reformat as bullet points")]
    [InlineData("change to markdown format")]
    [InlineData("make it a numbered list")]
    public async Task DetectAsync_WhenTransformationKeywordsPresent_ReturnsTransformFormat(string prompt)
    {
        var context = CreateContext(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.TransformFormat, result.Intent);
    }

    [Theory]
    [InlineData("what does this document say about revenue?")]
    [InlineData("when was this created?")]
    [InlineData("can you find details about the meeting?")]
    [InlineData("how does the product work?")]
    public async Task DetectAsync_WhenQuestionPattern_ReturnsDocumentQnA(string prompt)
    {
        var context = CreateContext(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.DocumentQnA, result.Intent);
    }

    [Fact]
    public async Task DetectAsync_WhenNoSpecificPattern_DefaultsToDocumentQnA()
    {
        var context = CreateContext("process the document content");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.DocumentQnA, result.Intent);
    }

    [Fact]
    public async Task DetectAsync_ReturnsConfidenceValue()
    {
        var context = CreateContext("summarize this document");

        var result = await _detector.DetectAsync(context);

        Assert.True(result.Confidence > 0);
        Assert.True(result.Confidence <= 1);
    }

    [Fact]
    public async Task DetectAsync_ReturnsReasonForIntent()
    {
        var context = CreateContext("summarize this document");

        var result = await _detector.DetectAsync(context);

        Assert.NotNull(result.Reason);
        Assert.NotEmpty(result.Reason);
    }

    private static DocumentIntentDetectionContext CreateContext(string prompt)
    {
        return new DocumentIntentDetectionContext
        {
            Prompt = prompt,
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
            }
        };
    }

    private static DocumentIntentDetectionContext CreateContextWithCsvDocument(string prompt)
    {
        return new DocumentIntentDetectionContext
        {
            Prompt = prompt,
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
            }
        };
    }

    private static DocumentIntentDetectionContext CreateContextWithMultipleDocuments(string prompt)
    {
        return new DocumentIntentDetectionContext
        {
            Prompt = prompt,
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents =
                [
                    new ChatInteractionDocument
                    {
                        DocumentId = "doc1",
                        FileName = "document1.txt",
                        ContentType = "text/plain",
                        Text = "First document content"
                    },
                    new ChatInteractionDocument
                    {
                        DocumentId = "doc2",
                        FileName = "document2.txt",
                        ContentType = "text/plain",
                        Text = "Second document content"
                    }
                ]
            }
        };
    }
}
