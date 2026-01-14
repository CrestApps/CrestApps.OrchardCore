namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Result of intent detection with the detected intent and confidence level.
/// </summary>
public sealed class DocumentIntentResult
{
    /// <summary>
    /// Gets or sets the detected document intent name.
    /// </summary>
    public required string Intent { get; set; }

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
    public static DocumentIntentResult FromIntent(string intent, float confidence = 1.0f, string reason = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(intent);

        return new DocumentIntentResult
        {
            Intent = intent,
            Confidence = confidence,
            Reason = reason,
        };
    }
}
