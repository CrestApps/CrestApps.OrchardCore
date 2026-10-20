using CrestApps.OrchardCore.Reviews.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Reviews.ViewModels;

// Not sealed: Orchard Core derives a proxy from a view model it initializes, and a sealed one renders nothing.
public class ProductReviewsViewModel
{
    public string ProductId { get; set; }

    public string ProductTitle { get; set; }

    public ProductRating Rating { get; set; } = ProductRating.None;

    public IReadOnlyList<ProductReview> Reviews { get; set; } = [];

    public bool IsSignedIn { get; set; }

    public bool CanReview { get; set; }

    public ProductReview Existing { get; set; }

    public string DefaultAuthorName { get; set; }

    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets whether buyers can add photos: the Media feature is on.
    /// </summary>
    public bool CanAddPhotos { get; set; }
}

public class ProductRatingViewModel
{
    public ProductRating Rating { get; set; } = ProductRating.None;
}

public class ReviewInputModel
{
    public int Rating { get; set; }

    public string Title { get; set; }

    public string Body { get; set; }

    public string AuthorName { get; set; }

    public string ReturnUrl { get; set; }

    public List<Microsoft.AspNetCore.Http.IFormFile> Photos { get; set; } = [];
}

public enum ReviewStatusFilter
{
    Pending,

    Approved,

    Rejected,

    All,
}

public class ReviewsIndexOptions
{
    public ReviewStatusFilter Status { get; set; }

    [BindNever]
    public IList<SelectListItem> Statuses { get; set; } = [];
}

public class ReviewsIndexViewModel
{
    public ReviewsIndexOptions Options { get; set; } = new();

    public dynamic Pager { get; set; }

    [BindNever]
    public AdminListContext List { get; set; }
}

public class ReviewSettingsViewModel
{
    public bool RequireApproval { get; set; }

    public bool BuyersOnly { get; set; }

    public bool SendReviewRequests { get; set; }

    public int ReviewRequestDelayDays { get; set; } = 14;
}
