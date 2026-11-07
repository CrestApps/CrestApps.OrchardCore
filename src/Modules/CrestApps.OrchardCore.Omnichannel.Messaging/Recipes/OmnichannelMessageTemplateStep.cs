using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;

/// <summary>
/// Imports messaging templates carried by a recipe step, creating entries that do not exist and updating those that do. An entry
/// is matched by its identifier alone, and a created entry keeps the identifier it was exported with so references to
/// it from other entries and steps keep resolving.
/// </summary>
internal sealed class OmnichannelMessageTemplateStep : CatalogRecipeStep<MessageTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageTemplateStep"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the messaging templates.</param>
    public OmnichannelMessageTemplateStep(IMessageTemplateManager manager)
        : base(MessagingDeploymentSteps.MessageTemplate, "Templates", manager)
    {
    }
}
