using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.Workflows;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Workflows;

/// <summary>
/// Starts when an agent has been out of adherence with their schedule for longer than their rule allows, once per
/// episode.
/// </summary>
public sealed class WorkforceOutOfAdherenceEvent : WorkforceEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceOutOfAdherenceEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceOutOfAdherenceEvent(IStringLocalizer<WorkforceOutOfAdherenceEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => WorkforceEvents.OutOfAdherence;

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Agent Out Of Adherence"];
}
