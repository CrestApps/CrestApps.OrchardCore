using CrestApps.OrchardCore.Surveys.Core.Controls.TextBlock;
using CrestApps.OrchardCore.Surveys.Core.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits the settings of text blocks in the designer. The HTML is sanitized when it is saved.
/// </summary>
public sealed class TextBlockSettingsDisplayDriver : SurveyControlSettingsDisplayDriver<TextBlockSettings>
{
    private readonly IHtmlSanitizerService _htmlSanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBlockSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="htmlSanitizer">The HTML sanitizer.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public TextBlockSettingsDisplayDriver(
        IHtmlSanitizerService htmlSanitizer,
        IStringLocalizer<TextBlockSettingsDisplayDriver> stringLocalizer)
        : base(stringLocalizer)
    {
        _htmlSanitizer = htmlSanitizer;
    }

    /// <inheritdoc />
    protected override string ControlType => SurveyControlNames.TextBlock;

    /// <inheritdoc />
    protected override LocalizedString CardName => S["Text Block Settings"];

    /// <inheritdoc />
    protected override Task UpdateSettingsAsync(SurveyQuestion question, TextBlockSettings settings, UpdateEditorContext context)
    {
        settings.Html = string.IsNullOrWhiteSpace(settings.Html) ? null : _htmlSanitizer.Sanitize(settings.Html);
        settings.ImageUrl = string.IsNullOrWhiteSpace(settings.ImageUrl) ? null : settings.ImageUrl.Trim();

        return Task.CompletedTask;
    }
}
