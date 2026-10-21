using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Classifieds.Models;

/// <summary>
/// Makes a content type something visitors post from the site: an ad, a directory entry, a property. It's moderated,
/// runs for a term, can be featured or bumped by a paid upgrade, and can have a place, for a search by distance.
/// </summary>
public sealed class ClassifiedPart : ContentPart
{
    public ClassifiedStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it stops being shown, or <see langword="null"/> while it waits to be approved.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets until when it's featured: shown first, with a badge.
    /// </summary>
    public DateTime? FeaturedUntilUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last bumped to the top, as if just posted.
    /// </summary>
    public DateTime? BumpedUtc { get; set; }

    /// <summary>
    /// Gets or sets where it is, as people read it, such as "Seattle, WA".
    /// </summary>
    public string Place { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string RejectReason { get; set; }

    public DateTime? PostedUtc { get; set; }
}

public enum ClassifiedStatus
{
    /// <summary>
    /// Posted, waiting for a moderator.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Shown until it expires.
    /// </summary>
    Approved = 1,

    Rejected = 2,

    /// <summary>
    /// Its term ran out; its poster can renew it.
    /// </summary>
    Expired = 3,
}

/// <summary>
/// How a content type's posted listings run.
/// </summary>
public sealed class ClassifiedPartSettings
{
    /// <summary>
    /// Gets or sets how many days a listing runs once it's approved, and again each time it's renewed.
    /// </summary>
    public int TermDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether a moderator approves each listing before it's shown.
    /// </summary>
    public bool RequireApproval { get; set; } = true;
}

/// <summary>
/// Makes a product an upgrade for a posted listing: buying it for a listing features it, or bumps it to the top.
/// </summary>
public sealed class ClassifiedUpgradePart : ContentPart
{
    public ClassifiedUpgradeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how many days a feature lasts.
    /// </summary>
    public int Days { get; set; } = 7;
}

public enum ClassifiedUpgradeKind
{
    Feature = 0,
    Bump = 1,
}
