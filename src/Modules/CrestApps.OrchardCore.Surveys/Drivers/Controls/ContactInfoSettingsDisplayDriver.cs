using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers.Controls;

/// <summary>
/// Edits which fields a contact information question asks for, and which are required.
/// </summary>
public sealed class ContactInfoSettingsDisplayDriver : DisplayDriver<SurveyQuestion>
{
    /// <summary>
    /// The field is not asked for.
    /// </summary>
    public const string ModeNone = "none";

    /// <summary>
    /// The field is optional.
    /// </summary>
    public const string ModeOptional = "optional";

    /// <summary>
    /// The field is required.
    /// </summary>
    public const string ModeRequired = "required";

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactInfoSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ContactInfoSettingsDisplayDriver(IStringLocalizer<ContactInfoSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => model is not null && string.Equals(model.ControlType, SurveyControlNames.ContactInfo, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion model, BuildEditorContext context)
        => Initialize<ContactInfoSettingsViewModel>($"{SurveyControlShapes.Settings}_Edit__{SurveyControlNames.ContactInfo}", viewModel =>
        {
            var settings = model.GetSettings<ContactInfoSettings>();

            viewModel.Fields = Enum.GetValues<ContactInfoFieldKind>()
                .Select(kind =>
                {
                    var field = settings.Fields.FirstOrDefault(item => item.Kind == kind);

                    return new ContactInfoFieldEditor
                    {
                        Kind = kind.ToString(),
                        Mode = field is null ? ModeNone : field.Required ? ModeRequired : ModeOptional,
                        Label = field?.Label,
                    };
                })
                .ToList();
        }).Location(l => l.Zone("Content", "20").Card(S["Contact Information Settings"], SurveyControlShapes.SettingsCardPosition));

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion model, UpdateEditorContext context)
    {
        var viewModel = new ContactInfoSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var fields = new List<ContactInfoField>();

        foreach (var editor in viewModel.Fields ?? [])
        {
            if (!Enum.TryParse<ContactInfoFieldKind>(editor?.Kind, out var kind) || editor.Mode is not (ModeOptional or ModeRequired) || fields.Any(field => field.Kind == kind))
            {
                continue;
            }

            fields.Add(new ContactInfoField
            {
                Kind = kind,
                Required = editor.Mode == ModeRequired,
                Label = string.IsNullOrWhiteSpace(editor.Label) ? null : editor.Label.Trim(),
            });
        }

        model.SetSettings(new ContactInfoSettings { Fields = fields });

        return Edit(model, context);
    }
}
