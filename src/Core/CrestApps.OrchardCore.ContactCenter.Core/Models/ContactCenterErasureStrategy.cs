namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes how a Contact Center data category satisfies an erasure or right-to-be-forgotten request.
/// </summary>
public enum ContactCenterErasureStrategy
{
    /// <summary>
    /// No erasure action is required because the category holds no personal data.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// The record is removed automatically when it ages past its retention window; no per-subject erasure is
    /// performed because the data is short-lived and bounded by retention.
    /// </summary>
    RetentionExpiry,

    /// <summary>
    /// The personal fields are cleared while the record is kept so aggregate metrics and audit history remain
    /// intact after the individual can no longer be identified.
    /// </summary>
    Anonymize,

    /// <summary>
    /// The record is erased as part of erasing the parent interaction it belongs to, rather than on its own.
    /// </summary>
    CascadeWithInteraction,

    /// <summary>
    /// The payload lives in an external system (a telephony provider or media store) and erasure is delegated
    /// to that system; Contact Center only holds a reference and records the delegated request.
    /// </summary>
    ExternalStore,
}
