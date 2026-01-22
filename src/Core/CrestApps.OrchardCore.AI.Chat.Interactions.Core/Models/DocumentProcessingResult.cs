#pragma warning disable MEAI001 // IImageGenerator is experimental but we intentionally use it

using System.Text;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Result of document processing containing additional context to inject into the AI completion.
/// This class allows multiple strategies to contribute context by adding to the AdditionalContexts list.
/// </summary>
public sealed class DocumentProcessingResult
{
    /// <summary>
    /// Gets the list of additional context entries to be appended to the system message.
    /// Multiple strategies can add context to this list.
    /// </summary>
    public List<string> AdditionalContexts { get; } = [];

    /// <summary>
    /// Gets or sets whether vector search was used in the processing.
    /// </summary>
    public bool UsedVectorSearch { get; set; }

    /// <summary>
    /// Gets whether any strategy has contributed context.
    /// </summary>
    public bool HasContext => AdditionalContexts.Count > 0;

    /// <summary>
    /// Gets or sets whether the processing was successful.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Gets or sets an error message if processing failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was for image generation intent.
    /// </summary>
    public bool IsImageGenerationIntent { get; set; }

    /// <summary>
    /// Gets or sets generated images if the intent was image generation.
    /// </summary>
    public ImageGenerationResponse GeneratedImages { get; set; }

    /// <summary>
    /// Gets whether any images were generated.
    /// </summary>
    public bool HasGeneratedImages => GeneratedImages?.Contents?.Count > 0;

    /// <summary>
    /// Adds context with an optional prefix message.
    /// </summary>
    /// <param name="context">The context content to add.</param>
    /// <param name="prefix">Optional prefix explaining the context.</param>
    /// <param name="usedVectorSearch">Whether vector search was used to obtain this context.</param>
    public void AddContext(string context, string prefix = null, bool usedVectorSearch = false)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return;
        }

        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            builder.AppendLine(prefix);
        }

        builder.Append(context);
        AdditionalContexts.Add(builder.ToString());

        if (usedVectorSearch)
        {
            UsedVectorSearch = true;
        }
    }

    /// <summary>
    /// Gets the combined additional context from all strategies.
    /// </summary>
    public string GetCombinedContext()
    {
        if (AdditionalContexts.Count == 0)
        {
            return string.Empty;
        }

        if (AdditionalContexts.Count == 1)
        {
            return AdditionalContexts[0];
        }

        return string.Join(Environment.NewLine + "---" + Environment.NewLine, AdditionalContexts);
    }

    /// <summary>
    /// Sets the result to a failed state with the specified error message.
    /// </summary>
    public void SetFailed(string errorMessage)
    {
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }
}
