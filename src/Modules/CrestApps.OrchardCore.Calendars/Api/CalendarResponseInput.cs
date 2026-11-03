namespace CrestApps.OrchardCore.Calendars.Api;

/// <summary>
/// An answer to an invitation sent to the API.
/// </summary>
public sealed class CalendarResponseInput
{
    /// <summary>
    /// Gets or sets the answer: Accepted, Tentative or Declined.
    /// </summary>
    public string Response { get; set; }
}
