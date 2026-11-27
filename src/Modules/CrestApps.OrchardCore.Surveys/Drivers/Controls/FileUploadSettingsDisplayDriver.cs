using CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of FileUpload questions in the designer.
/// </summary>
public sealed class FileUploadSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<FileUploadSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileUploadSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public FileUploadSettingsDisplayDriver(IStringLocalizer<FileUploadSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.FileUpload;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["File Upload Settings"];
}
