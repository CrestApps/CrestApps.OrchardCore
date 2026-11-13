using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Checks.Workflows;

/// <summary>
/// Starts when the bank paid a check.
/// </summary>
public sealed class CheckClearedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckClearedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckClearedEvent(IStringLocalizer<CheckClearedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckClearedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Cleared"];
}
