using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Processes uploaded files into AI documents and embedded chunks.
/// </summary>
public interface IAIDocumentProcessingService
{
    /// <summary>
    /// Processes an uploaded file by extracting text, chunking, generating embeddings, and creating an AI document.
    /// </summary>
    Task<DocumentProcessingResult> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator);
}
