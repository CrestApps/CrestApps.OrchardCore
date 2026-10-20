using CrestApps.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Memberships.Models;

/// <summary>
/// Makes a product a membership: buying it puts the buyer in its roles for its length, so content and features
/// gated by those roles open to them.
/// </summary>
public sealed class MembershipPart : ContentPart
{
    /// <summary>
    /// Gets or sets the roles a member is in.
    /// </summary>
    public string[] RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days one unit lasts, or zero for a membership that doesn't end. Buying more units, or
    /// buying again while a member, adds their days.
    /// </summary>
    public int DurationDays { get; set; } = 365;
}

/// <summary>
/// A member: the roles a paid order put them in, from when and until when.
/// </summary>
public sealed class Membership : CatalogItem
{
    public string UserId { get; set; }

    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the membership product.
    /// </summary>
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public string[] RoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the roles it put its member in: those they weren't in already, which are the ones taken away when it
    /// ends.
    /// </summary>
    public List<string> AddedRoleNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the orders that bought or extended it, newest last.
    /// </summary>
    public List<string> OrderIds { get; set; } = [];

    public DateTime StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, or <see langword="null"/> for one that doesn't.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    public MembershipStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets why it ended early, such as the order being refunded.
    /// </summary>
    public string EndedReason { get; set; }
}

public enum MembershipStatus
{
    Active = 0,

    /// <summary>
    /// It ran out.
    /// </summary>
    Expired = 1,

    /// <summary>
    /// It was ended early: its order was refunded or canceled, or the store ended it.
    /// </summary>
    Revoked = 2,
}
