using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the general, presentation and navigation settings of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionGeneralDisplayDriver : DisplayDriver<SurveyDefinition>
{
    private readonly IHtmlSanitizerService _htmlSanitizer;

    public SurveyDefinitionGeneralDisplayDriver(IHtmlSanitizerService htmlSanitizer)
    {
        _htmlSanitizer = htmlSanitizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Initialize<SurveyDefinitionGeneralEditViewModel>("SurveyDefinitionGeneral_Edit", model =>
        {
            model.DefinitionTitle = definition.Title;
            model.Introduction = definition.Introduction;
            model.Presentation = definition.Presentation ?? new SurveyPresentationSettings();
            model.Navigation = definition.Navigation ?? new SurveyNavigationSettings();
        }).Location("Content:1#General");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyDefinitionGeneralEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        definition.Title = model.DefinitionTitle?.Trim();
        definition.Introduction = string.IsNullOrWhiteSpace(model.Introduction) ? null : _htmlSanitizer.Sanitize(model.Introduction);
        definition.Presentation = model.Presentation ?? new SurveyPresentationSettings();
        definition.Presentation.QuestionsPerPage = Math.Max(0, definition.Presentation.QuestionsPerPage);
        definition.Navigation = model.Navigation ?? new SurveyNavigationSettings();

        if (definition.Navigation.Sequential)
        {
            definition.Navigation.AllowBack = false;
        }

        return Edit(definition, context);
    }
}
