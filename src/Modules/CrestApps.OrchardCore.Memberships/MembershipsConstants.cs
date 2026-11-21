namespace CrestApps.OrchardCore.Memberships;

public static class MembershipsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Memberships";

    /// <summary>
    /// The YesSql collection that holds memberships.
    /// </summary>
    public const string CollectionName = "Memberships";

    /// <summary>
    /// The site settings group of the membership settings (Settings › Commerce › Memberships).
    /// </summary>
    public const string SettingsGroupId = "memberships";

    public static class Recipes
    {
        /// <summary>
        /// The recipe step that adds or updates memberships.
        /// </summary>
        public const string Memberships = "Memberships";
    }
}
