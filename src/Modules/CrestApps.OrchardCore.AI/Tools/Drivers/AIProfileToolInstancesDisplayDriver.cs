using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Tools.Services;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on AI profiles, allowing administrators
/// to choose which instances the profile exposes to the AI model.
/// </summary>
internal sealed class AIProfileToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    public AIProfileToolInstancesDisplayDriver(IAIToolInstanceAccessor instanceAccessor)
        : base(instanceAccessor)
    {
    }
}
