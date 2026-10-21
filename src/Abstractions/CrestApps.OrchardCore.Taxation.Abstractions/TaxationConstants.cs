namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Provides shared constant values for the taxation framework.
/// </summary>
public static class TaxationConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the taxation module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the core taxation feature.
        /// </summary>
        public const string Taxation = "CrestApps.OrchardCore.Taxation";

        /// <summary>
        /// Works out sales tax with TaxJar for the countries it's set up for.
        /// </summary>
        public const string TaxJar = "CrestApps.OrchardCore.Taxation.TaxJar";
    }

    /// <summary>
    /// Contains the content part names used by the taxation framework.
    /// </summary>
    public static class Parts
    {
        /// <summary>
        /// The technical name of the taxation content part.
        /// </summary>
        public const string TaxationPart = nameof(TaxationPart);
    }

    /// <summary>
    /// The admin settings group identifier for taxation related settings.
    /// </summary>
    public const string SettingsGroupId = "taxation";
}
