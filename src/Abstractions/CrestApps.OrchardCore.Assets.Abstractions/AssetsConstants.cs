namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// The constants of the Assets module.
/// </summary>
public static class AssetsConstants
{
    /// <summary>
    /// The YesSql collection assets are stored in.
    /// </summary>
    public const string CollectionName = "Assets";

    /// <summary>
    /// The source of the calendars and the owner kind of the stock locations assets own.
    /// </summary>
    public const string Source = "Asset";

    /// <summary>
    /// The calendar type of a schedulable asset, from the Calendars Resources feature.
    /// </summary>
    public const string EquipmentCalendarType = "Equipment";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Assets: types, assets, hierarchy, meters and warranty.
        /// </summary>
        public const string Assets = "CrestApps.OrchardCore.Assets";

        /// <summary>
        /// Fleet: vehicles and equipment as schedulable resources, with van stock.
        /// </summary>
        public const string Fleet = "CrestApps.OrchardCore.Assets.Fleet";
    }
}
