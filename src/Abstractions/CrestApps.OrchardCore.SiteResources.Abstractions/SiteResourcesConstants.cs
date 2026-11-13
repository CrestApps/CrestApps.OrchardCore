namespace CrestApps.OrchardCore.SiteResources;

/// <summary>
/// Names shared by the Site Resources module and the modules that integrate with it.
/// </summary>
public static class SiteResourcesConstants
{
    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries site resources.
    /// </summary>
    public const string RecipeStepName = "SiteResources";

    /// <summary>
    /// The path, under the tenant, that serves the published site resources.
    /// </summary>
    public const string RoutePrefix = "site-resources";

    /// <summary>
    /// The resource manager type of a stylesheet.
    /// </summary>
    public const string StylesheetResourceType = "stylesheet";

    /// <summary>
    /// The resource manager type of a script.
    /// </summary>
    public const string ScriptResourceType = "script";

    /// <summary>
    /// The feature identifiers of the Site Resources module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Site Resources feature.
        /// </summary>
        public const string SiteResources = "CrestApps.OrchardCore.SiteResources";
    }
}
