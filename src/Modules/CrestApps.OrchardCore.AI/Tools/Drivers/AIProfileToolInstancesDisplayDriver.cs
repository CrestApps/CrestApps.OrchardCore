using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Tools.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on AI profiles, allowing administrators
/// to choose which instances the profile exposes to the AI model.
/// </summary>
internal sealed class AIProfileToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfile>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileToolInstancesDisplayDriver(
        IAIToolInstanceAccessor instanceAccessor,
        IStringLocalizer<AIProfileToolInstancesDisplayDriver> stringLocalizer)
        : base(instanceAccessor)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override string EditorLocation => new PlacementLocationBuilder()
        .Zone("Content", "7")
        .Tab(S["Capabilities"], "8")
        .Card(S["Agents & Tools"], "10")
        .ToString();
}
