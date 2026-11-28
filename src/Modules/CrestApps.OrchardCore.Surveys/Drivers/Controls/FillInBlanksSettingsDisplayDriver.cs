using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of FillInBlanks questions in the designer.
/// </summary>
public sealed class FillInBlanksSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<FillInBlanksSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FillInBlanksSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public FillInBlanksSettingsDisplayDriver(IStringLocalizer<FillInBlanksSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.FillInBlanks;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Fill in the Blanks Settings"];
}
