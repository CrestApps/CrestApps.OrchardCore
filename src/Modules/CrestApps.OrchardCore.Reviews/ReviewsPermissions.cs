using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reviews;

public static class ReviewsPermissions
{
    public static readonly Permission ManageReviews = new(
        "ManageProductReviews",
        LocalizationSource.Create("Moderate product reviews", typeof(ReviewsPermissions)));

    public static readonly Permission SubmitReviews = new(
        "SubmitProductReviews",
        LocalizationSource.Create("Review products bought", typeof(ReviewsPermissions)));
}
