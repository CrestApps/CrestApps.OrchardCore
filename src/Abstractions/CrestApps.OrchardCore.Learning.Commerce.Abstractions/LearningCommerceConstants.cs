namespace CrestApps.OrchardCore.Learning.Commerce;

/// <summary>
/// The names Learning commerce uses.
/// </summary>
public static class LearningCommerceConstants
{
    /// <summary>
    /// The feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Learning.Commerce";

    /// <summary>
    /// The YesSql collection of purchases, licence pools and seats.
    /// </summary>
    public const string CollectionName = "LearningCommerce";

    /// <summary>
    /// The YesSql collection of the seat ledger.
    /// </summary>
    public const string SeatLedgerCollectionName = "LearningSeatLedger";

    /// <summary>
    /// The enrollment grant source of a course bought for oneself; the source id is the order id and line id.
    /// </summary>
    public const string PurchaseSource = "Learning.Commerce";

    /// <summary>
    /// The enrollment grant source of a seat given from a licence pool; the source id is the seat.
    /// </summary>
    public const string SeatSource = "Learning.LicenseSeat";

    /// <summary>
    /// The data source name.
    /// </summary>
    public const string DataSourceName = "LearningCommerce";

    /// <summary>
    /// The notification type of a seat given to a person.
    /// </summary>
    public const string SeatGivenNotificationType = "LearningSeatGiven";
}
