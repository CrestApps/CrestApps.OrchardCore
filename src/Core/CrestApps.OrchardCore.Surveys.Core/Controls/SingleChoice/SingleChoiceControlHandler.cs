using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;

/// <summary>
/// One choice from a list.
/// </summary>
public sealed class SingleChoiceControlHandler : SurveyChoiceControlHandlerBase<SingleChoiceSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SingleChoiceControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SingleChoiceControlHandler(IStringLocalizer<SingleChoiceControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.SingleChoice;
}
