using CrestApps.Core.AI;
using CrestApps.OrchardCore.Calendars.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the calendar AI tools, which administrators can pick on AI profiles.
/// </summary>
[Feature(CalendarsConstants.Features.AITools)]
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
        services.AddCoreAITool<ListCalendarsTool>(ListCalendarsTool.ToolName)
            .WithTitle(S["List calendars"])
            .WithDescription(S["Lists the calendars the user can see."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<FindCalendarEventsTool>(FindCalendarEventsTool.ToolName)
            .WithTitle(S["Find calendar events"])
            .WithDescription(S["Lists the events on the user's calendars between two times."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<FindFreeTimeTool>(FindFreeTimeTool.ToolName)
            .WithTitle(S["Find free time"])
            .WithDescription(S["Finds times when the user and others are free."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<CreateCalendarEventTool>(CreateCalendarEventTool.ToolName)
            .WithTitle(S["Create calendar event"])
            .WithDescription(S["Adds an event to one of the user's calendars."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<MoveCalendarEventTool>(MoveCalendarEventTool.ToolName)
            .WithTitle(S["Move calendar event"])
            .WithDescription(S["Moves an event to another time."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<CancelCalendarEventTool>(CancelCalendarEventTool.ToolName)
            .WithTitle(S["Cancel calendar event"])
            .WithDescription(S["Cancels an event."])
            .WithCategory(S["Calendar"])
            .Selectable();
    }
}
