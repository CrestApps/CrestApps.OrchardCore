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

    /// <summary>
    /// The name of the recipe and deployment step that sets stock counts.
    /// </summary>
    public const string StockRecipeStep = "Stock";

    public static class Features
    {
        public const string Inventory = "CrestApps.OrchardCore.Inventory";

        public const string StockAlerts = "CrestApps.OrchardCore.Inventory.StockAlerts";

        public const string PurchaseOrders = "CrestApps.OrchardCore.Inventory.PurchaseOrders";
    }

    public static class Parts
    {
        public const string InventoryPart = "InventoryPart";
    }
}
