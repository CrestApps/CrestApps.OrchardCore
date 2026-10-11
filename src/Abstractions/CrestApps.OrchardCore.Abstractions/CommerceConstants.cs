namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Provides shared constant values for the Commerce module that owns the common Commerce admin menu.
/// </summary>
public static class CommerceConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the Commerce module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Commerce feature that registers the shared Commerce admin menu and its icon. It is enabled
        /// by dependency only, so any module that contributes to the Commerce menu should depend on it.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Commerce";
    }
}
