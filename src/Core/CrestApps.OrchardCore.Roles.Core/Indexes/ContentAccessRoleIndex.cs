using YesSql.Indexes;

namespace CrestApps.OrchardCore.Roles.Core.Indexes;

/// <summary>
/// The roles allowed to read each published item of a content type whose <c>RolePickerPart</c> restricts it (the
/// Content Access Control feature keeps it): one row per picked role. An item with no role picked is open to everyone,
/// so it gets two rows, <c>Anonymous</c> and <c>Authenticated</c>, and a reader's roles from
/// <see cref="ContentAccessRoles.GetReaderRoles"/> always meet it. Items of types that are not restricted and drafts
/// get no row.
/// </summary>
/// <remarks>
/// A list or a search of a restricted type keeps to what its reader may open in SQL rather than authorizing each item:
/// <c>index.ContentItemId.IsIn&lt;ContentAccessRoleIndex&gt;(row =&gt; row.ContentItemId, row =&gt; row.RoleName.IsIn(roles))</c>
/// against an index of published items,
/// skipped for a reader who <see cref="ContentAccessRoles.SeesEverything"/>.
/// </remarks>
public sealed class ContentAccessRoleIndex : MapIndex
{
    /// <summary>
    /// The longest role name kept.
    /// </summary>
    public const int MaxRoleNameLength = 255;

    /// <summary>
    /// Gets or sets the item.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the item's content type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets a role allowed to read the item.
    /// </summary>
    public string RoleName { get; set; }
}
