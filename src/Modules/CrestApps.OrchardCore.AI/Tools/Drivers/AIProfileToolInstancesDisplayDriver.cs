using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;

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
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public AIProfileToolInstancesDisplayDriver(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
        : base(instancesCatalog, toolAccessEvaluator, httpContextAccessor)
    {
    }
}
