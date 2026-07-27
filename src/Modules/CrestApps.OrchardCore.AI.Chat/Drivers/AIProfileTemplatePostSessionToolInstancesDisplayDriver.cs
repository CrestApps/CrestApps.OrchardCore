using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Chat.Drivers;

/// <summary>
/// Display driver that lets administrators choose which AI tool instances are available to the AI model
/// while an AI profile template runs its post-session processing tasks.
/// </summary>
internal sealed class AIProfileTemplatePostSessionToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfileTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplatePostSessionToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public AIProfileTemplatePostSessionToolInstancesDisplayDriver(
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
    protected override bool CanHandle(AIProfileTemplate model)
    {
        return model.Source == AITemplateSources.Profile;
    }

    /// <inheritdoc/>
    protected override string[] GetSelectedInstanceNames(AIProfileTemplate model)
    {
        return model.GetOrCreate<AIProfilePostSessionSettings>().ToolInstanceNames ?? [];
    }

    /// <inheritdoc/>
    protected override void SetSelectedInstanceNames(AIProfileTemplate model, string[] instanceNames)
    {
        var settings = model.GetOrCreate<AIProfilePostSessionSettings>();

        settings.ToolInstanceNames = instanceNames;

        model.Put(settings);
    }
}
