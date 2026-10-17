namespace CrestApps.OrchardCore.Inventory.Core;

public static class InventoryConstants
{
    /// <summary>
    /// The YesSql collection stock items, movements and their indexes live in.
    /// </summary>
    public const string CollectionName = "Inventory";

    /// <summary>
    /// The location every stock item belongs to until stores can have several.
    /// </summary>
    public const string DefaultLocationId = "default";

    /// <summary>
    /// The site settings group of the Inventory settings.
    /// </summary>
    public const string SettingsGroupId = "inventory";

    public static class Features
    {
        public const string Inventory = "CrestApps.OrchardCore.Inventory";
    }

    public static class Parts
    {
        public const string InventoryPart = "InventoryPart";
    }
}
