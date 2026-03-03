using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class DocumentProcessingResult
{
    public bool Success { get; private set; }

    public AIDocument Document { get; private set; }

    public ChatDocumentInfo DocumentInfo { get; private set; }

    public string Error { get; private set; }

    public static DocumentProcessingResult Succeeded(AIDocument document, ChatDocumentInfo documentInfo)
    {
        return new DocumentProcessingResult
        {
            Success = true,
            Document = document,
            DocumentInfo = documentInfo,
        };
    }

    public static DocumentProcessingResult Failed(string error)
    {
        return new DocumentProcessingResult
        {
            Success = false,
            Error = error,
        };
    }
}
