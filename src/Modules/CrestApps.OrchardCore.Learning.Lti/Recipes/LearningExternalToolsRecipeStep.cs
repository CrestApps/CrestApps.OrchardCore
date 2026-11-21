using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Recipes;

/// <summary>
/// Imports external tool settings from the <c>LearningExternalTools</c> step's <c>Tools</c>, by item id. The tool's
/// OpenID application is imported with Orchard Core's OpenID application step.
/// </summary>
internal sealed class LearningExternalToolsRecipeStep : CatalogRecipeStep<ExternalTool>
{
    public LearningExternalToolsRecipeStep(IExternalToolManager manager)
        : base(LtiConstants.RecipeStepName, "Tools", manager)
    {
    }
}
