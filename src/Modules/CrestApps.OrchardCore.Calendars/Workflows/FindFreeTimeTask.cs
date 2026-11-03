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
/// Finds times when calendars and users are all free.
/// </summary>
public sealed class FindFreeTimeTask : CalendarTaskActivity<FindFreeTimeTask>
{
    private readonly ICalendarAvailabilityService _availabilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindFreeTimeTask"/> class.
    /// </summary>
    /// <param name="availabilityService">The free/busy service.</param>
    /// <param name="workflowService">The calendar workflow service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FindFreeTimeTask(
        ICalendarAvailabilityService availabilityService,
        CalendarWorkflowService workflowService,
        IStringLocalizer<FindFreeTimeTask> stringLocalizer)
        : base(workflowService, stringLocalizer)
    {
        _availabilityService = availabilityService;
    }

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Find Free Time"];

    /// <summary>
    /// Gets or sets the calendar ids field, a Liquid expression.
    /// </summary>
    public string CalendarIds
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }
    /// <summary>
    /// Gets or sets the user ids field, a Liquid expression.
    /// </summary>
    public string UserIds
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
    /// <summary>
    /// Gets or sets the length in minutes field, a Liquid expression.
    /// </summary>
    public string DurationMinutes
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
        if (!DateTimeOffset.TryParse(await Calendars.EvaluateAsync(From, workflowContext), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from) ||
            !DateTimeOffset.TryParse(await Calendars.EvaluateAsync(To, workflowContext), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to) ||
            !int.TryParse(await Calendars.EvaluateAsync(DurationMinutes, workflowContext), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ||
            minutes < 1)
        {
            return Outcome("None");
        }

        var windows = await _availabilityService.FindFreeWindowsAsync(new CalendarBusyQuery
        {
            CalendarIds = CalendarWorkflowService.Split(await Calendars.EvaluateAsync(CalendarIds, workflowContext)),
            UserIds = CalendarWorkflowService.Split(await Calendars.EvaluateAsync(UserIds, workflowContext)),
            FromUtc = from.UtcDateTime,
            ToUtc = to.UtcDateTime,
        }, TimeSpan.FromMinutes(minutes));

        var output = windows.Select(window => new Dictionary<string, object>
        {
            ["StartUtc"] = CalendarWorkflowInput.Format(window.StartUtc),
            ["EndUtc"] = CalendarWorkflowInput.Format(window.EndUtc),
        }).ToList();

        workflowContext.Output[GetOutputName("FreeTimes")] = output;
        workflowContext.LastResult = output;

        return Outcome(output.Count == 0 ? "None" : "Found");
    }
}
