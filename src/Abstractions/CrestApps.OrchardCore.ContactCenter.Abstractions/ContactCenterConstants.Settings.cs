namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Contains site-settings configuration identifiers used by the Contact Center module set.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// The site settings group identifier used for Contact Center administrative configuration.
        /// Every Contact Center settings display driver must use this group identifier so all
        /// Contact Center settings appear together on the same settings screen.
        /// </summary>
        public const string GroupId = "contactcenter";
    }
}
