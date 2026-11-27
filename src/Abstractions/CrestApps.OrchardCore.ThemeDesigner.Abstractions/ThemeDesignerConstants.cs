namespace CrestApps.OrchardCore.ThemeDesigner;

/// <summary>
/// Names shared by the Theme Designer module and the modules that integrate with it.
/// </summary>
public static class ThemeDesignerConstants
{
    /// <summary>
    /// The path, under the tenant, that serves the site resources of the signed-in designer's draft.
    /// </summary>
    public const string DraftResourcesRoutePrefix = "theme-designer/resources";

    /// <summary>
    /// The feature identifiers of the Theme Designer module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Theme Designer feature.
        /// </summary>
        public const string ThemeDesigner = "CrestApps.OrchardCore.ThemeDesigner";
    }
}
