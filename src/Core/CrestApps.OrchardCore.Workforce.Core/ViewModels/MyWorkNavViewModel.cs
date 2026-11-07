using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.ViewModels;

/// <summary>
/// The portal's navigation.
/// </summary>
public class MyWorkNavViewModel
{
    /// <summary>
    /// Gets or sets the page shown.
    /// </summary>
    public string Active { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets whether the user is a worker.
    /// </summary>
    public bool IsWorker { get; set; }

    /// <summary>
    /// Gets or sets how many notifications are unread.
    /// </summary>
    public int Unread { get; set; }

    /// <summary>
    /// Gets or sets whether shift planning is on.
    /// </summary>
    public bool HasShiftPlanning { get; set; }

    /// <summary>
    /// Gets or sets whether the worker can request time off here.
    /// </summary>
    public bool HasTimeOff { get; set; }

    /// <summary>
    /// Gets or sets whether the user manages teams.
    /// </summary>
    public bool IsManager { get; set; }

    /// <summary>
    /// Gets or sets whether the worker clocks in and out, so the timesheet is shown.
    /// </summary>
    public bool HasTimeClock { get; set; }

    /// <summary>
    /// Gets or sets the links other features add to the menu.
    /// </summary>
    public IList<MyWorkLink> Links { get; set; } = [];
}
