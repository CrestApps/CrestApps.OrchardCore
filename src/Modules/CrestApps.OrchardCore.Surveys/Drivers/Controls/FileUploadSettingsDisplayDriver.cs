using CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;
using CrestApps.OrchardCore.Surveys.Core.Drivers;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of FileUpload questions in the designer.
/// </summary>
public sealed class FileUploadSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<FileUploadSettings>
{
    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.FileUpload;
}
