using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Deployments;

namespace CrestApps.OrchardCore.Telephony.Recipes;

/// <summary>
/// Imports internal extensions carried by a recipe step, creating entries that do not exist and updating those that do.
/// An entry is matched by its identifier alone, and a created entry keeps the identifier it was exported with. The user
/// an extension rings is resolved by user name by the entry's handler, because user identifiers differ between
/// environments.
/// </summary>
internal sealed class TelephonyExtensionStep : CatalogRecipeStep<TelephonyExtension>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the internal extensions.</param>
    public TelephonyExtensionStep(ITelephonyExtensionManager manager)
        : base(TelephonyDeploymentSteps.Extension, "Extensions", manager)
    {
    }
}
