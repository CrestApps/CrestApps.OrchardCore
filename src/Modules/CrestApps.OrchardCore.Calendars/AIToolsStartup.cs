using CrestApps.Core.AI;
using CrestApps.OrchardCore.Calendars.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the calendar AI tools, which administrators can pick on AI profiles, whenever the AI feature is on.
/// </summary>
[Feature(CalendarsConstants.Features.Calendars)]
[RequireFeatures("CrestApps.OrchardCore.AI")]
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

/// <summary>
/// Registers the AI tools that manage calendars, their events, answers and categories, whenever the AI feature is on.
/// </summary>
[Feature(CalendarsConstants.Features.Calendars)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class CalendarManagementAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarManagementAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarManagementAIToolsStartup(IStringLocalizer<CalendarManagementAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetCalendarTool>(GetCalendarTool.ToolName)
            .WithTitle(S["Get calendar"])
            .WithDescription(S["Gets one calendar: type, owner, time zone, visibility, enabled, overlap limit, room or office details, its event types, who it is shared with, whether it has a feed address, and its subscription state and last sync error. Use it before changing a calendar."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<SaveCalendarTool>(SaveCalendarTool.ToolName)
            .WithTitle(S["Save calendar"])
            .WithDescription(S["Creates a calendar of a given type or updates one: name, description, color, time zone, visibility, enabled, owner (user or role), overlap limit and capacity, and room, equipment or office details. It does not change sharing, the feed or the subscription."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<ShareCalendarTool>(ShareCalendarTool.ToolName)
            .WithTitle(S["Share calendar"])
            .WithDescription(S["Replaces who a calendar is shared with: users and roles at free/busy, read, write or manage level. Read the current shares with getCalendar first, because the list you give replaces them."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsSecurity();

        services.AddCoreAITool<DeleteCalendarTool>(DeleteCalendarTool.ToolName)
            .WithTitle(S["Delete calendar"])
            .WithDescription(S["Deletes a calendar and every event on it. Refused for a calendar a module manages (appointments, workforce teams, fleet assets)."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<GetCalendarEventTool>(GetCalendarEventTool.ToolName)
            .WithTitle(S["Get calendar event"])
            .WithDescription(S["Gets one event in full: type, times and time zone, recurrence, status, visibility, category, reminders, each attendee with their answer, and whether the user can change it. Use it before updateCalendarEvent."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<UpdateCalendarEventTool>(UpdateCalendarEventTool.ToolName)
            .WithTitle(S["Update calendar event"])
            .WithDescription(S["Changes an event's title, type, times, time zone, location, description, status, busy/free, visibility, category, reminders, recurrence or attendees; for a repeating event say which occurrences (this one, this and following, all). Use moveCalendarEvent when only the time changes."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<DeleteCalendarEventTool>(DeleteCalendarEventTool.ToolName)
            .WithTitle(S["Delete calendar event"])
            .WithDescription(S["Permanently deletes an event (the whole series for a repeating event). Prefer cancelCalendarEvent, which keeps the event and tells the attendees."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsDestructive();

        services.AddCoreAITool<RespondToCalendarEventTool>(RespondToCalendarEventTool.ToolName)
            .WithTitle(S["Respond to calendar event"])
            .WithDescription(S["Records the user's own answer to an invitation: accepted, tentative or declined. It cannot answer for someone else."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsWrite()
            .RequireApproval();

        services.AddCoreAITool<ListCalendarCategoriesTool>(ListCalendarCategoriesTool.ToolName)
            .WithTitle(S["List calendar categories"])
            .WithDescription(S["Lists the event categories the user can use (their own and the shared ones) with id, name and color. Use it to find a category id for an event."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<SaveCalendarCategoryTool>(SaveCalendarCategoryTool.ToolName)
            .WithTitle(S["Save calendar category"])
            .WithDescription(S["Creates or updates an event category: name, color (#rrggbb) and whether everyone shares it."])
            .WithCategory(S["Calendar"])
            .Selectable();

        services.AddCoreAITool<DeleteCalendarCategoryTool>(DeleteCalendarCategoryTool.ToolName)
            .WithTitle(S["Delete calendar category"])
            .WithDescription(S["Deletes an event category. Events that used it keep their own color and lose the category."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsDestructive();
    }
}

/// <summary>
/// Registers the team availability AI tool when Team Calendars and the AI feature are on.
/// </summary>
[Feature(CalendarsConstants.Features.Teams)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class TeamCalendarsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeamCalendarsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TeamCalendarsAIToolsStartup(IStringLocalizer<TeamCalendarsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetTeamAvailabilityTool>(GetTeamAvailabilityTool.ToolName)
            .WithTitle(S["Get team availability"])
            .WithDescription(S["For a team calendar and a day, lists each team member's busy times and the times everyone is free for a meeting of the given length. Needs the Team Calendars feature."])
            .WithCategory(S["Calendar"])
            .Selectable();
    }
}

/// <summary>
/// Registers the AI tool that resets a calendar's private subscription address when Calendar Feeds and the AI feature are
/// on.
/// </summary>
[Feature(CalendarsConstants.Features.Feeds)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class CalendarFeedsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarFeedsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarFeedsAIToolsStartup(IStringLocalizer<CalendarFeedsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ResetCalendarFeedAddressTool>(ResetCalendarFeedAddressTool.ToolName)
            .WithTitle(S["Reset calendar feed address"])
            .WithDescription(S["Makes a new private subscription address for a calendar, or removes it. The old address stops working at once, so everyone subscribed must be given the new one."])
            .WithCategory(S["Calendar"])
            .Selectable()
            .AsSecurity();
    }
}
