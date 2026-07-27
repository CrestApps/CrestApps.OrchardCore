namespace CrestApps.OrchardCore.ContactCenter.Maintenance;

/// <summary>
/// Reports the outcome of quiescing Contact Center work admission across the enabled Contact Center features.
/// </summary>
public sealed class ContactCenterPreviewQuiesceReport
{
    /// <summary>
    /// Gets the feature identifiers whose work admission was closed.
    /// </summary>
    public required IReadOnlyList<string> QuiescedFeatureIds { get; init; }

    /// <summary>
    /// Gets the feature identifiers whose in-flight work did not finish before the drain timeout elapsed.
    /// </summary>
    public required IReadOnlyList<string> DrainTimedOutFeatureIds { get; init; }

    /// <summary>
    /// Gets a value indicating whether every quiesced feature drained within the timeout.
    /// </summary>
    public bool IsDrained => DrainTimedOutFeatureIds.Count == 0;
}
