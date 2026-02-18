using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public interface IAIDocumentProcessingService
{
    /// <summary>
    /// Creates an embedding generator for the given provider and connection.
    /// Returns <see langword="null"/> if no embedding deployment is configured or the generator cannot be created.
    /// </summary>
    Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName);

    /// <summary>
    /// Processes an uploaded file by extracting text, chunking, generating embeddings, and creating an <see cref="AI.Models.AIDocument"/>.
    /// </summary>
    Task<DocumentProcessingResult> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator);
}
