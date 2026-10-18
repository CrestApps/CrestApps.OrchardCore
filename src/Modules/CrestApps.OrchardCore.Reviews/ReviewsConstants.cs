namespace CrestApps.OrchardCore.Reviews;

public static class ReviewsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Reviews";

    /// <summary>
    /// The YesSql collection the reviews are kept in.
    /// </summary>
    public const string CollectionName = "Reviews";

    public const string SettingsGroupId = "reviews";

    public const int MaxRating = 5;

    public const int MaxTitleLength = 120;

    public const int MaxBodyLength = 4000;
}
