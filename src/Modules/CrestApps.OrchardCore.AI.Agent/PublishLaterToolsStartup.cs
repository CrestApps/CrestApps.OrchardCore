using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.PublishLater;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that schedules a content item to be published or archived, when the Publish Later feature is on.
/// The Archive Later feature registers the same tool, so the tool is there when either feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.PublishLater)]
public sealed class PublishLaterToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishLaterToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PublishLaterToolsStartup(IStringLocalizer<PublishLaterToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ScheduleContentItemTool>(ScheduleContentItemTool.TheName)
            .WithTitle(S["Schedule Content Item"])
            .WithDescription(S["Schedules a content item to be published or archived (unpublished) at a date and time given in the site's time zone, or cancels the schedule. The content type must have the Publish Later or Archive Later part; publishing is scheduled on a draft, archiving on a published item."])
            .WithCategory(S["Content Management"])
            .Selectable();
    }
}
