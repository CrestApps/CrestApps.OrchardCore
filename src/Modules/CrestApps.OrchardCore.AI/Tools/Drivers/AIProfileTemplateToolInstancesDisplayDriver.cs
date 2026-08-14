using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Tools.Services;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on AI profile templates so a template
/// can seed the instances of every profile created from it.
/// </summary>
internal sealed class AIProfileTemplateToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfileTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    public AIProfileTemplateToolInstancesDisplayDriver(IAIToolInstanceAccessor instanceAccessor)
        : base(instanceAccessor)
    {
    }

    protected override bool CanHandle(AIProfileTemplate template)
        => template.Source == AITemplateSources.Profile;
}
