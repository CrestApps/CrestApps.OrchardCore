using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Core.ChatInteractions;

public sealed class KeywordPromptIntentDetectorTests
{
    private readonly KeywordPromptIntentDetector _detector = new();

    [Fact]
    public async Task DetectAsync_WhenPromptIsEmpty_ReturnsGeneralChatWithReference()
    {
        var context = CreateContext("");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GeneralChatWithReference, result.Name);
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

        Assert.Equal(DocumentIntents.SummarizeDocument, result.Name);
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

        Assert.Equal(DocumentIntents.AnalyzeTabularData, result.Name);
    }

    [Theory]
    [InlineData("for every row, output the reason for escalation")]
    [InlineData("for each row return whether escalation is present")]
    [InlineData("per row extract the verbatim escalation quote")]
    public async Task DetectAsync_WhenRowLevelKeywordsWithCsvFile_ReturnsAnalyzeTabularDataByRow(string prompt)
    {
        var context = CreateContextWithCsvDocument(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.AnalyzeTabularDataByRow, result.Name);
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

        Assert.Equal(DocumentIntents.ExtractStructuredData, result.Name);
    }

    [Theory]
    [InlineData("compare these two documents")]
    [InlineData("what is the difference between them?")]
    [InlineData("how are they similar?")]
    public async Task DetectAsync_WhenComparisonKeywordsWithMultipleDocs_ReturnsCompareDocuments(string prompt)
    {
        var context = CreateContextWithMultipleDocuments(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.CompareDocuments, result.Name);
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

        Assert.Equal(DocumentIntents.TransformFormat, result.Name);
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

        Assert.Equal(DocumentIntents.DocumentQnA, result.Name);
    }

    [Fact]
    public async Task DetectAsync_WhenNoSpecificPattern_DefaultsToDocumentQnA()
    {
        var context = CreateContext("process the document content");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.DocumentQnA, result.Name);
    }

    [Theory]
    [InlineData("generate an image of a sunset")]
    [InlineData("create an image of a cat")]
    [InlineData("draw a landscape")]
    [InlineData("generate a picture of mountains")]
    [InlineData("create a visual of the concept")]
    public async Task DetectAsync_WhenImageGenerationKeywordsPresent_ReturnsGenerateImage(string prompt)
    {
        var context = CreateContextWithNoDocuments(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GenerateImage, result.Name);
    }

    [Theory]
    [InlineData("draw an bar chart representing that data")]
    [InlineData("create a bar chart")]
    [InlineData("generate a chart image")]
    [InlineData("make a chart")]
    [InlineData("create a graph")]
    public async Task DetectAsync_WhenChartKeywordsPresent_ReturnsGenerateChart(string prompt)
    {
        var context = CreateContextWithNoDocuments(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GenerateChart, result.Name);
    }

    [Theory]
    [InlineData("use that data to create a chart")]
    [InlineData("based on this, generate a bar chart")]
    [InlineData("create a chart from the above")]
    public async Task DetectAsync_WhenChartKeywordsReferenceHistory_ReturnsGenerateChart(string prompt)
    {
        var context = CreateContextWithNoDocuments(prompt);

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GenerateChart, result.Name);
    }

    [Fact]
    public async Task DetectAsync_WhenImageKeywordsReferenceHistory_ReturnsGenerateImageWithHistory()
    {
        var context = CreateContextWithNoDocuments("generate an image from that table");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GenerateImageWithHistory, result.Name);
    }

    [Fact]
    public async Task DetectAsync_ImageGenerationTakesPriority_OverDocumentQnA()
    {
        // Image generation should work even without documents
        var context = CreateContextWithNoDocuments("generate an image of a beautiful garden");

        var result = await _detector.DetectAsync(context);

        Assert.Equal(DocumentIntents.GenerateImage, result.Name);
        Assert.True(result.Confidence >= 0.9f);
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
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "document.txt",
                        ContentType = "text/plain",
                        FileSize = 100
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
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "data.csv",
                        ContentType = "text/csv",
                        FileSize = 100
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
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc1",
                        FileName = "document1.txt",
                        ContentType = "text/plain",
                        FileSize = 100
                    },
                    new ChatInteractionDocumentInfo
                    {
                        DocumentId = "doc2",
                        FileName = "document2.txt",
                        ContentType = "text/plain",
                        FileSize = 100
                    }
                ]
            }
        };
    }

    private static DocumentIntentDetectionContext CreateContextWithNoDocuments(string prompt)
    {
        return new DocumentIntentDetectionContext
        {
            Prompt = prompt,
            Interaction = new ChatInteraction
            {
                ItemId = "test-id",
                Documents = []
            }
        };
    }
}
