namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Why a calendar operation was refused.
/// </summary>
/// <param name="Field">The field the error is about, such as <c>Start</c>, or empty for the whole record.</param>
/// <param name="Message">The localized message to show.</param>
/// <param name="Code">A stable code callers can act on, such as <c>conflict</c>.</param>
public sealed record CalendarError(string Field, string Message, string Code = null)
{
    /// <summary>
    /// The code of an error caused by another event already taking the time.
    /// </summary>
    public const string ConflictCode = "conflict";

    /// <summary>
    /// The code of an error caused by the caller not being allowed to do the change.
    /// </summary>
    public const string ForbiddenCode = "forbidden";
}
