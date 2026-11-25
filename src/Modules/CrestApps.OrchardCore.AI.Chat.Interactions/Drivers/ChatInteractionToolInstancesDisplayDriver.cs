using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on chat interactions, allowing
/// administrators to choose which instances the interaction exposes to the AI model.
/// </summary>
internal sealed class ChatInteractionToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<ChatInteraction>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChatInteractionToolInstancesDisplayDriver(
        IAIToolInstanceAccessor instanceAccessor,
        IStringLocalizer<ChatInteractionToolInstancesDisplayDriver> stringLocalizer)
        : base(instanceAccessor)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string EditorShapeType => "ChatInteractionToolInstances_Edit";

    /// <inheritdoc/>
    protected override string EditorLocation => new PlacementLocationBuilder()
        .Zone("Parameters", "8")
        .Tab(S["Capabilities"], "3")
        .Card(S["Agents & Tools"], "10")
        .ToString();
}
