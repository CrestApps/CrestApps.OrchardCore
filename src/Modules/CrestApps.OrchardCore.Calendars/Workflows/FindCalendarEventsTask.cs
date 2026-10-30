using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Lists the occurrences on some calendars between two times.
/// </summary>
public sealed class FindCalendarEventsTask : CalendarTaskActivity<FindCalendarEventsTask>
{
    private readonly ICalendarQueryService _queryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindCalendarEventsTask"/> class.
    /// </summary>
    /// <param name="queryService">The occurrence query service.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FindCalendarEventsTask(
        ICalendarQueryService queryService,
        CalendarWorkflowService workflowService,
        IStringLocalizer<FindCalendarEventsTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _queryService = queryService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Find Calendar Events"];

    /// <summary>
    /// Gets or sets the calendar ids field, a Liquid expression.
    /// </summary>
    public string CalendarIds
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the from field, a Liquid expression.
    /// </summary>
    public string From
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the to field, a Liquid expression.
    /// </summary>
    public string To
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <inheritdoc />
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Found"]), new Outcome(S["None"])];
    }

    /// <inheritdoc />
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var calendarIds = CalendarWorkflowService.Split(await Calendars.EvaluateAsync(CalendarIds, workflowContext));

        if (calendarIds.Count == 0 ||
            !DateTimeOffset.TryParse(await Calendars.EvaluateAsync(From, workflowContext), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from) ||
            !DateTimeOffset.TryParse(await Calendars.EvaluateAsync(To, workflowContext), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to))
        {
            return Outcome("None");
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = calendarIds,
            FromUtc = from.UtcDateTime,
            ToUtc = to.UtcDateTime,
        });

        var output = occurrences.Take(500).Select(occurrence =>
        {
            var item = CalendarWorkflowInput.ToDictionary(occurrence.Event);

            item["OccurrenceStartUtc"] = CalendarWorkflowInput.Format(occurrence.StartUtc);
            item["OccurrenceEndUtc"] = CalendarWorkflowInput.Format(occurrence.EndUtc);

            return item;
        }).ToList();

        workflowContext.Output[GetOutputName("CalendarEvents")] = output;
        workflowContext.LastResult = output;

        return Outcome(output.Count == 0 ? "None" : "Found");
    }
}
