using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeZones;

/// <summary>
/// Provides constant values for the Time Zones module.
/// </summary>
public static class TimeZonesConstants
{
    /// <summary>
    /// Contains feature identifiers for the Time Zones module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The feature identifier for the Time Zones module.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.TimeZones";
    }

    /// <summary>
    /// Contains recipe step names for the Time Zones module.
    /// </summary>
    public static class Recipes
    {
        /// <summary>
        /// The recipe step name for importing time zone maps.
        /// </summary>
        public const string TimeZoneMaps = "TimeZoneMaps";
    }

    /// <summary>
    /// Contains permission definitions for the Time Zones module.
    /// </summary>
    public static class Permissions
    {
        /// <summary>
        /// Gets the permission to manage time zone maps.
        /// </summary>
        public static readonly Permission ManageTimeZoneMaps = new("ManageTimeZoneMaps", "Manage time zone maps");
    }
}
