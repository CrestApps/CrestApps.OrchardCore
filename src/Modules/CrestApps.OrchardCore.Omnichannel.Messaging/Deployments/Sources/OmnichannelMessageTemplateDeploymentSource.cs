using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;

/// <summary>
/// Exports every stored entry of the messaging templates catalog, whole, so a member added to the record later travels without a
/// change here.
/// </summary>
internal sealed class OmnichannelMessageTemplateDeploymentSource : CatalogDeploymentSource<OmnichannelMessageTemplateDeploymentStep, MessageTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the messaging templates.</param>
    public OmnichannelMessageTemplateDeploymentSource(IMessageTemplateManager manager)
        : base("Templates", manager)
    {
    }
}
