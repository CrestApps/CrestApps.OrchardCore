using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using CrestApps.OrchardCore.Core.Recipes;

namespace CrestApps.OrchardCore.ContactCenter.Recipes;

/// <summary>
/// Imports voice media clips carried by a recipe step, creating entries that do not exist and updating those that do. An entry
/// is matched by its identifier alone, and a created entry keeps the identifier it was exported with so references to
/// it from other entries and steps keep resolving.
/// </summary>
internal sealed class ContactCenterVoiceMediaStep : CatalogRecipeStep<VoiceMediaItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the voice media clips.</param>
    public ContactCenterVoiceMediaStep(IVoiceMediaItemManager manager)
        : base(ContactCenterDeploymentSteps.VoiceMedia, "VoiceMedia", manager)
    {
    }
}
