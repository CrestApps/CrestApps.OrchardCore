namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Well-known document processing intent names.
/// </summary>
public static class DocumentIntents
{
    public const string DocumentQnA = "DocumentQnA";

    public const string SummarizeDocument = "SummarizeDocument";

    public const string AnalyzeTabularData = "AnalyzeTabularData";

    public const string AnalyzeTabularDataByRow = "AnalyzeTabularDataByRow";

    public const string ExtractStructuredData = "ExtractStructuredData";

    public const string CompareDocuments = "CompareDocuments";

    public const string TransformFormat = "TransformFormat";

    public const string GeneralChatWithReference = "GeneralChatWithReference";

    public const string GenerateImage = "GenerateImage";

    public const string GenerateImageWithHistory = "GenerateImageWithHistory";

    public const string GenerateChart = "GenerateChart";
}
