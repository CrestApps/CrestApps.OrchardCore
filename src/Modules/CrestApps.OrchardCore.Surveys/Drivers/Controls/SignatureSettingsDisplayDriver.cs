using CrestApps.OrchardCore.Surveys.Core.Controls.Signature;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Signature questions in the designer.
/// </summary>
public sealed class SignatureSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SignatureSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SignatureSettingsDisplayDriver(IStringLocalizer<SignatureSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Signature;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Signature Settings"];
}
