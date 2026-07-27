using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Tools.Services;

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
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    public ChatInteractionToolInstancesDisplayDriver(IAIToolInstanceAccessor instanceAccessor)
        : base(instanceAccessor)
    {
    }

    /// <inheritdoc/>
    protected override string EditorShapeType => "ChatInteractionToolInstances_Edit";

    /// <inheritdoc/>
    protected override string EditorLocation => "Parameters:8#Capabilities;3";
}
