using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Result of intent detection with the detected intent and confidence level.
/// </summary>
public sealed class DocumentIntentResult
{
    /// <summary>
    /// Gets or sets the detected document intent.
    /// </summary>
    public DocumentIntent Intent { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the detection (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets an optional reason or explanation for the detected intent.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Creates a result with high confidence for a specific intent.
    /// </summary>
    public static DocumentIntentResult FromIntent(DocumentIntent intent, float confidence = 1.0f, string reason = null)
    {
        return new DocumentIntentResult
        {
            Intent = intent,
            Confidence = confidence,
            Reason = reason,
        };
    }
}
