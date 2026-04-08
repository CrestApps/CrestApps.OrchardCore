using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Represents the result of processing an uploaded AI document.
/// </summary>
public sealed class DocumentProcessingResult
{
    public bool Success { get; private set; }

    public AIDocument Document { get; private set; }

    public ChatDocumentInfo DocumentInfo { get; private set; }

    public IReadOnlyList<AIDocumentChunk> Chunks { get; private set; }

    public string Error { get; private set; }

    public static DocumentProcessingResult Succeeded(AIDocument document, ChatDocumentInfo documentInfo, IReadOnlyList<AIDocumentChunk> chunks)
    {
        return new DocumentProcessingResult
        {
            Success = true,
            Document = document,
            DocumentInfo = documentInfo,
            Chunks = chunks ?? [],
        };
    }

    public static DocumentProcessingResult Failed(string error)
    {
        return new DocumentProcessingResult
        {
            Success = false,
            Error = error,
            Chunks = [],
        };
    }
}
