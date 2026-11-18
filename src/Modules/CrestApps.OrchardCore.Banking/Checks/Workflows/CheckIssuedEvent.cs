using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts when a check is issued with its number.
/// </summary>
public sealed class CheckIssuedEvent : CheckEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckIssuedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckIssuedEvent(IStringLocalizer<CheckIssuedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(CheckIssuedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Check Issued"];
}
