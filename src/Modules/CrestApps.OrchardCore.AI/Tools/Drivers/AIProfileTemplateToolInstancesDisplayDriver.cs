using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;

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
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public AIProfileTemplateToolInstancesDisplayDriver(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
        : base(instancesCatalog, toolAccessEvaluator, httpContextAccessor)
    {
    }

    protected override bool CanHandle(AIProfileTemplate template)
        => template.Source == AITemplateSources.Profile;
}
