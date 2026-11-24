using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a check is voided, or when a misprinted sheet of a print run is kept as a spoiled check. A stopped or
/// reissued check starts Check Stop Payment or Check Reissued instead.
/// </summary>
public sealed class CheckVoidedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckVoidedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckVoidedEvent(IStringLocalizer<CheckVoidedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckVoidedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Voided"];
}
