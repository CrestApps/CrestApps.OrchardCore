using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a cleared check is recorded as not paid after all and goes back to the status it cleared from.
/// </summary>
public sealed class CheckUnclearedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckUnclearedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckUnclearedEvent(IStringLocalizer<CheckUnclearedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckUnclearedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Uncleared"];
}
