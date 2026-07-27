using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that lets administrators choose which AI tool instances are available to the AI model
/// while an AI profile runs its post-session processing tasks.
/// </summary>
internal sealed class AIProfilePostSessionToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfile>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfilePostSessionToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public AIProfilePostSessionToolInstancesDisplayDriver(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
        : base(instancesCatalog, toolAccessEvaluator, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    protected override string EditorShapeType => "PostSessionToolInstances_Edit";

    /// <inheritdoc/>
    protected override string EditorLocation => "Content:11#Data Processing & Metrics;10";

    /// <inheritdoc/>
    protected override string[] GetSelectedInstanceNames(AIProfile model)
    {
        return model.GetSettings<AIProfilePostSessionSettings>().ToolInstanceNames ?? [];
    }

    /// <inheritdoc/>
    protected override void SetSelectedInstanceNames(AIProfile model, string[] instanceNames)
    {
        model.AlterSettings<AIProfilePostSessionSettings>(settings => settings.ToolInstanceNames = instanceNames);
    }
}
