using CrestApps.Core.AI;
using CrestApps.OrchardCore.Timeline.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Timeline;

/// <summary>
/// Adds the Timeline AI tools when the AI feature is on: reading the history of any record with a timeline, and adding
/// an internal note to it. Each tool acts as the signed-in person, with the access the record's module gives on the
/// timeline card.
/// </summary>
[Feature(TimelineConstants.Features.Timeline)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetTimelineTool>(GetTimelineTool.TheName)
            .WithTitle(S["Get timeline"])
            .WithDescription(S["Reads the history of one record, newest first: its notes, comments, replies and events, with who wrote them and when, for an account, contact or lead, an order, a ticket, a work order or any record with a timeline. Only the entries the user may read."])
            .WithCategory(S["Timeline"])
            .Selectable();

        services.AddCoreAITool<AddTimelineNoteTool>(AddTimelineNoteTool.TheName)
            .WithTitle(S["Add timeline note"])
            .WithDescription(S["Adds an internal note, which only staff read, to the timeline of an account, contact, lead, order, work order or other record, under the user's name, as the record's Notes card does. An @username in it notifies that user."])
            .WithCategory(S["Timeline"])
            .Selectable();
    }
}
