using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Tools.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Display driver that presents the configured AI tool instances on AI profile templates so a template
/// can seed the instances of every profile created from it.
/// </summary>
internal sealed class AIProfileTemplateToolInstancesDisplayDriver : AIToolInstancesDisplayDriverBase<AIProfileTemplate>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateToolInstancesDisplayDriver"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIProfileTemplateToolInstancesDisplayDriver(
        IAIToolInstanceAccessor instanceAccessor,
        IStringLocalizer<AIProfileTemplateToolInstancesDisplayDriver> stringLocalizer)
        : base(instanceAccessor)
    {
        S = stringLocalizer;
    }

    protected override bool CanHandle(AIProfileTemplate template)
        => template.Source == AITemplateSources.Profile;

    /// <inheritdoc/>
    protected override string EditorLocation => new PlacementLocationBuilder()
        .Zone("Content", "7")
        .Tab(S["Capabilities"], "8")
        .Card(S["Agents & Tools"], "10")
        .ToString();
}
