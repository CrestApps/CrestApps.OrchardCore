namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an AI-suggested disposition for an interaction.
/// </summary>
public sealed class DispositionSuggestion
{
    /// <summary>
    /// Gets or sets the identifier of the suggested disposition.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the suggested disposition.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the confidence of the suggestion, from 0 to 1.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets a short rationale for the suggestion.
    /// </summary>
    public string Rationale { get; set; }
}
