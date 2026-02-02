namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Models;

/// <summary>
/// Detected intent metadata.
/// </summary>
public sealed class DocumentIntent
{
    public required string Name { get; set; }

    public float Confidence { get; set; }

    public string Reason { get; set; }

    public static DocumentIntent FromName(string name, float confidence = 1.0f, string reason = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return new DocumentIntent
        {
            Name = name,
            Confidence = confidence,
            Reason = reason,
        };
    }
}
