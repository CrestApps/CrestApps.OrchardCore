namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Contains constant values for the DNC Registry module.
/// </summary>
public static class DncRegistryConstants
{
    /// <summary>
    /// The YesSql collection name for DNC registry documents.
    /// </summary>
    public const string CollectionName = "DNC";

    /// <summary>
    /// Represents the features.
    /// </summary>
    public static class Features
    {
        public const string Area = "CrestApps.OrchardCore.DncRegistry";

        public const string UsaFtc = "CrestApps.OrchardCore.DncRegistry.UsaFtc";

        public const string CanadaDncl = "CrestApps.OrchardCore.DncRegistry.CanadaDncl";

        public const string Local = "CrestApps.OrchardCore.DncRegistry.Local";
    }

    /// <summary>
    /// Represents the settings group identifiers.
    /// </summary>
    public static class SettingsGroupIds
    {
        public const string ImportContentSettings = "importContentSettings";

        public const string UsaFtcRegistry = "usaFtcRegistry";

        public const string CanadaDnclRegistry = "canadaDnclRegistry";
    }
}
