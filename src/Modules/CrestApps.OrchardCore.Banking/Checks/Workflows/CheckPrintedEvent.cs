using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a check is printed.
/// </summary>
public sealed class CheckPrintedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckPrintedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckPrintedEvent(IStringLocalizer<CheckPrintedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckPrintedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Printed"];
}
