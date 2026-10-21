using CrestApps.OrchardCore.Classifieds.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Classifieds.ViewModels;

/// <summary>
/// The Classified part's editor and display. Coordinates are text, read in the invariant culture.
/// </summary>
public class ClassifiedPartViewModel
{
    public ClassifiedStatus Status { get; set; }

    public string Place { get; set; }

    public string Latitude { get; set; }

    public string Longitude { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public DateTime? FeaturedUntilUtc { get; set; }

    [BindNever]
    public string RejectReason { get; set; }

    [BindNever]
    public bool IsFeatured { get; set; }

    [BindNever]
    public bool IsAdmin { get; set; }
}

public class ClassifiedPartSettingsViewModel
{
    public int TermDays { get; set; }

    public bool RequireApproval { get; set; }
}

public class ClassifiedUpgradePartViewModel
{
    public ClassifiedUpgradeKind Kind { get; set; }

    public int Days { get; set; }
}

/// <summary>
/// A posted listing as the moderation list shows it.
/// </summary>
public sealed class ClassifiedEntry
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string Title { get; set; }

    public string OwnerName { get; set; }

    public ClassifiedPart Part { get; set; }
}

public class ClassifiedsAdminIndexViewModel
{
    public string Search { get; set; }

    public ClassifiedStatus? Status { get; set; }

    [BindNever]
    public AdminListContext<ClassifiedEntry> List { get; set; }
}

public class ClassifiedPostViewModel
{
    public string ContentType { get; set; }

    public string DisplayName { get; set; }

    public string ContentItemId { get; set; }

    public IShape Editor { get; set; }

    public bool RequireApproval { get; set; }
}

public class ClassifiedOwnViewModel
{
    public IList<ContentItem> Listings { get; set; } = [];

    public IList<(string ContentType, string DisplayName)> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the upgrades for sale: the product, its title and what it does.
    /// </summary>
    public IList<(string ContentItemId, string Title, ClassifiedUpgradePart Upgrade)> Upgrades { get; set; } = [];
}

public class ClassifiedSearchViewModel
{
    public string ContentType { get; set; }

    public string Text { get; set; }

    public string Latitude { get; set; }

    public string Longitude { get; set; }

    public int Kilometers { get; set; } = 25;

    public IList<(string ContentType, string DisplayName)> Types { get; set; } = [];

    public IList<(ContentItem Item, double? Kilometers, string Url)> Results { get; set; } = [];

    public bool Searched { get; set; }
}
