using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a worker clocks in.
/// </summary>
public sealed class WorkforcePunchedInEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforcePunchedInEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforcePunchedInEvent(IStringLocalizer<WorkforcePunchedInEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.PunchedIn;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Punched In"];
}
