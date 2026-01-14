namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Result of document processing containing additional context to inject into the AI completion.
/// </summary>
public sealed class DocumentProcessingResult
{
    /// <summary>
    /// Gets or sets additional context to be appended to the system message.
    /// This context is injected into the AI model to provide document-aware responses.
    /// </summary>
    public string AdditionalContext { get; set; }

    /// <summary>
    /// Gets or sets a prefix message to prepend to the context explaining what it contains.
    /// </summary>
    public string ContextPrefix { get; set; }

    /// <summary>
    /// Gets or sets whether vector search was used in the processing.
    /// </summary>
    public bool UsedVectorSearch { get; set; }

    /// <summary>
    /// Gets or sets whether the strategy handled the request.
    /// When false, the next strategy in the chain will be tried.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was successful.
    /// Only relevant when <see cref="Handled"/> is true.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Gets or sets an error message if processing failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful result with the specified context.
    /// </summary>
    public static DocumentProcessingResult Success(string context, string prefix = null, bool usedVectorSearch = false)
    {
        return new DocumentProcessingResult
        {
            AdditionalContext = context,
            ContextPrefix = prefix,
            UsedVectorSearch = usedVectorSearch,
            Handled = true,
            IsSuccess = true,
        };
    }

    /// <summary>
    /// Creates an empty successful result (no additional context needed).
    /// </summary>
    public static DocumentProcessingResult Empty()
    {
        return new DocumentProcessingResult
        {
            Handled = true,
            IsSuccess = true,
        };
    }

    /// <summary>
    /// Creates a result indicating the strategy did not handle the request.
    /// The next strategy in the chain will be tried.
    /// </summary>
    public static DocumentProcessingResult NotHandled()
    {
        return new DocumentProcessingResult
        {
            Handled = false,
            IsSuccess = true,
        };
    }

    /// <summary>
    /// Creates a failed result with the specified error message.
    /// </summary>
    public static DocumentProcessingResult Failed(string errorMessage)
    {
        return new DocumentProcessingResult
        {
            Handled = true,
            IsSuccess = false,
            ErrorMessage = errorMessage,
        };
    }
}
