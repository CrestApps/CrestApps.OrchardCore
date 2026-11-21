using CrestApps.OrchardCore.Surveys.Core.Controls.Signature;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of Signature questions in the designer.
/// </summary>
public sealed class SignatureSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<SignatureSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.Signature;
}
