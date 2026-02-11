using System.Text;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Result of document processing containing additional context to inject into the AI completion.
/// This class allows multiple strategies to contribute context by adding to the AdditionalContexts list.
/// </summary>
public sealed class IntentProcessingResult
{
    /// <summary>
    /// Gets the list of additional context entries to be appended to the system message.
    /// Multiple strategies can add context to this list.
    /// </summary>
    public List<string> AdditionalContexts { get; } = [];

    /// <summary>
    /// Gets the list of tool names that strategies request to be available for this completion
    /// request. These are merged into the completion context's <see cref="AICompletionContext.ToolNames"/>
    /// by the caller, and resolved by the standard tool registration pipeline.
    /// </summary>
    public List<string> ToolNames { get; } = [];

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
#pragma warning disable MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public ImageGenerationResponse GeneratedImages { get; set; }
#pragma warning restore MEAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    /// <summary>
    /// Gets whether any images were generated.
    /// </summary>
    public bool HasGeneratedImages => GeneratedImages?.Contents?.Count > 0;

    /// <summary>
    /// Gets or sets the detected intent name for this processing run.
    /// </summary>
    public string Intent { get; set; }

    /// <summary>
    /// Gets or sets the confidence level for the detected intent.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the reason/explanation for the detected intent.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets whether the processing was for chart generation intent.
    /// </summary>
    public bool IsChartGenerationIntent { get; set; }

    /// <summary>
    /// Gets or sets the generated Chart.js configuration JSON if the intent was chart generation.
    /// </summary>
    public string GeneratedChartConfig { get; set; }

    /// <summary>
    /// Gets whether a chart configuration was generated.
    /// </summary>
    public bool HasGeneratedChart => !string.IsNullOrWhiteSpace(GeneratedChartConfig);

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
