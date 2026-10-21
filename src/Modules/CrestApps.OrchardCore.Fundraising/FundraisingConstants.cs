namespace CrestApps.OrchardCore.Fundraising;

public static class FundraisingConstants
{
    public const string Feature = "CrestApps.OrchardCore.Fundraising";

    /// <summary>
    /// The YesSql collection that holds donations.
    /// </summary>
    public const string CollectionName = "Fundraising";

    public const string SettingsGroupId = "fundraising";

    /// <summary>
    /// The cart line detail holding the amount a donor chose.
    /// </summary>
    public const string AmountDetail = "donation.amount";

    /// <summary>
    /// The cart line detail naming who a donation honors.
    /// </summary>
    public const string TributeDetail = "donation.tribute";
}
