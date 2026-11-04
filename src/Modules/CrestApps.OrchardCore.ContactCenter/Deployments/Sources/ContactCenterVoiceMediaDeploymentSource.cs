using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments.Steps;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.ContactCenter.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the voice media clips catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class ContactCenterVoiceMediaDeploymentSource : CatalogDeploymentSource<ContactCenterVoiceMediaDeploymentStep, VoiceMediaItem>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceMediaDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the voice media clips.</param>
    public ContactCenterVoiceMediaDeploymentSource(IVoiceMediaItemManager manager)
        : base("VoiceMedia", manager)
    {
    }
}
