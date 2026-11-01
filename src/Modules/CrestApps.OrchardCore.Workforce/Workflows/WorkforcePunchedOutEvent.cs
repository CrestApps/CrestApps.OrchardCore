using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a worker clocks out.
/// </summary>
public sealed class WorkforcePunchedOutEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforcePunchedOutEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforcePunchedOutEvent(IStringLocalizer<WorkforcePunchedOutEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.PunchedOut;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Punched Out"];
}
