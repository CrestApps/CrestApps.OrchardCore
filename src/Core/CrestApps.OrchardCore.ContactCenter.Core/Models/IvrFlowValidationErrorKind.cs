namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The ways an IVR flow can be unrunnable.
/// </summary>
public enum IvrFlowValidationErrorKind
{
    /// <summary>
    /// The flow names no root menu, so a caller would hear nothing.
    /// </summary>
    RootNodeMissing,

    /// <summary>
    /// The flow names a root menu that is not among its menus.
    /// </summary>
    RootNodeNotFound,

    /// <summary>
    /// A menu has no identifier, so nothing can refer to it.
    /// </summary>
    NodeIdMissing,

    /// <summary>
    /// Two menus share an identifier, so a reference to it is ambiguous.
    /// </summary>
    NodeIdDuplicate,

    /// <summary>
    /// A menu has neither a spoken prompt nor a media prompt, so the caller would hear silence.
    /// </summary>
    NodePromptMissing,

    /// <summary>
    /// A menu offers no keys, so the caller can never leave it.
    /// </summary>
    NodeHasNoOptions,

    /// <summary>
    /// An option's key is not a single telephone key.
    /// </summary>
    OptionDigitInvalid,

    /// <summary>
    /// Two options on the same menu answer the same key.
    /// </summary>
    OptionDigitDuplicate,

    /// <summary>
    /// An option has no action, so pressing its key does nothing.
    /// </summary>
    OptionActionMissing,

    /// <summary>
    /// An action that needs a destination names none.
    /// </summary>
    ActionTargetMissing,

    /// <summary>
    /// A sub-menu action names a menu that is not among the flow's menus.
    /// </summary>
    SubMenuNotFound,

    /// <summary>
    /// The retry count allows no attempt at all.
    /// </summary>
    MaxRetriesInvalid,
}
