using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.Workflows;

/// <summary>
/// Starts when a shift with nobody is offered to the team.
/// </summary>
public sealed class WorkforceOpenShiftPostedEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceOpenShiftPostedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceOpenShiftPostedEvent(IStringLocalizer<WorkforceOpenShiftPostedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.OpenShiftPosted;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Open Shift Posted"];
}
