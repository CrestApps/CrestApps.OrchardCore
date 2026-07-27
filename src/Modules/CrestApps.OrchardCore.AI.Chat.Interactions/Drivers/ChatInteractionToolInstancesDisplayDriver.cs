using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on chat interactions, allowing
/// administrators to choose which instances the interaction exposes to the AI model.
/// </summary>
internal sealed class ChatInteractionToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<ChatInteraction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The tool instances catalog.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public ChatInteractionToolInstancesDisplayDriver(
        ISourceCatalog<AIToolInstance> instancesCatalog,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
        : base(instancesCatalog, toolAccessEvaluator, httpContextAccessor)
    {
    }

    /// <inheritdoc/>
    protected override string EditorShapeType => "ChatInteractionToolInstances_Edit";

    /// <inheritdoc/>
    protected override string EditorLocation => "Parameters:8#Capabilities;3";
}
