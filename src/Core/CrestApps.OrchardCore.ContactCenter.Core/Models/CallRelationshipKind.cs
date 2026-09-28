namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Identifies how one call session relates to another.
/// </summary>
public enum CallRelationshipKind
{
    /// <summary>
    /// The related session handed this call over to this session.
    /// </summary>
    TransferredFrom,

    /// <summary>
    /// This session handed the call over to the related session.
    /// </summary>
    TransferredTo,

    /// <summary>
    /// The related session is the private consult placed from this session.
    /// </summary>
    ConsultOf,

    /// <summary>
    /// The related session was merged into the same conference as this session.
    /// </summary>
    ConferencedWith,

    /// <summary>
    /// This session is the callback that fulfills the related session.
    /// </summary>
    CallbackOf,
}
