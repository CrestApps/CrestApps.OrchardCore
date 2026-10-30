using System.Globalization;
using System.Net.Mail;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// The helpers the calendar workflow tasks share: evaluating their Liquid fields and reading times and attendees from text.
/// </summary>
public sealed class CalendarWorkflowService
{
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarWorkflowService"/> class.
    /// </summary>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    public CalendarWorkflowService(IWorkflowExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// Evaluates a Liquid field.
    /// </summary>
    /// <param name="expression">The field.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The value, trimmed, or <see langword="null"/> when the field or its value is empty.</returns>
    public async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var value = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, null))?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Reads a time: an ISO 8601 time with an offset is a moment, converted to the event's time zone; one without an offset is
    /// a wall-clock time in the event's time zone.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="timeZoneId">The event's time zone.</param>
    /// <param name="local">The wall-clock time.</param>
    /// <returns><see langword="true"/> when the text is a time.</returns>
    public static bool TryParseTime(string value, string timeZoneId, out DateTime local)
    {
        local = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        var hasOffset = text.EndsWith('Z') || text.EndsWith('z') || (text.Length > 19 && (text[19] == '+' || text[19] == '-'));

        if (hasOffset && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
        {
            local = CalendarTime.ToLocal(moment.UtcDateTime, timeZoneId);

            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wallClock))
        {
            local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads attendees from text: one per line or comma, as an email, <c>Name &lt;email&gt;</c>, or <c>user:{userId}</c>.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The attendees.</returns>
    public static IList<CalendarAttendee> ParseAttendees(string value)
    {
        var result = new List<CalendarAttendee>();

        foreach (var item in (value ?? string.Empty).Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.StartsWith("user:", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new CalendarAttendee { UserId = item.Substring(5).Trim(), Role = CalendarAttendeeRole.Required });
            }
            else if (MailAddress.TryCreate(item, out var address))
            {
                result.Add(new CalendarAttendee
                {
                    Email = address.Address,
                    Name = string.IsNullOrWhiteSpace(address.DisplayName) ? null : address.DisplayName,
                    Role = CalendarAttendeeRole.Required,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Splits a comma or line separated list.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The items.</returns>
    public static IList<string> Split(string value)
    {
        return (value ?? string.Empty).Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
    }
}
