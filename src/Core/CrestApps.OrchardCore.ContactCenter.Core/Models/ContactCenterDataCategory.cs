namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes the data-governance classification of a single Contact Center persisted data category, including
/// its privacy sensitivity, retention basis, erasure approach, and whether it references call recordings.
/// </summary>
public sealed class ContactCenterDataCategory
{
    /// <summary>
    /// Gets the stable, machine-readable key that identifies this data category.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Gets the human-readable display name for this data category.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the privacy sensitivity classification for this data category.
    /// </summary>
    public required ContactCenterDataSensitivity Sensitivity { get; init; }

    /// <summary>
    /// Gets a value indicating whether this data category references call recordings whose payload is stored in
    /// an external provider or media store.
    /// </summary>
    public required bool ContainsRecordingReference { get; init; }

    /// <summary>
    /// Gets the retention basis describing what governs how long this data category is kept.
    /// </summary>
    public required string RetentionBasis { get; init; }

    /// <summary>
    /// Gets the erasure strategy that satisfies a right-to-be-forgotten request for this data category.
    /// </summary>
    public required ContactCenterErasureStrategy ErasureStrategy { get; init; }

    /// <summary>
    /// Gets a description of the data category and the reasoning behind its classification.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether this data category holds personal data, derived from its sensitivity.
    /// </summary>
    public bool ContainsPersonalData => Sensitivity != ContactCenterDataSensitivity.NonPersonal;
}
