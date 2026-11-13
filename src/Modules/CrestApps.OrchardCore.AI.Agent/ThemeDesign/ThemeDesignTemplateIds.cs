namespace CrestApps.OrchardCore.AI.Agent.ThemeDesign;

/// <summary>
/// The prompt templates the theme design tools use, by the id they are discovered under.
/// </summary>
/// <remarks>
/// A template's id is its file name without the extension, and the files live in <c>Templates/Prompts</c> in the
/// module, so a site can override them like any other prompt template.
/// </remarks>
public static class ThemeDesignTemplateIds
{
    /// <summary>
    /// How Orchard Core themes, shape templates and site resources fit together, returned by the overview tool.
    /// </summary>
    public const string Guide = "theme-design-guide";
}
