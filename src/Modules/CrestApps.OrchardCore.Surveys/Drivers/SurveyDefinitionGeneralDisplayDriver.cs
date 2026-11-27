using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
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

    internal readonly IStringLocalizer S;

    public SurveyDefinitionGeneralDisplayDriver(
        IHtmlSanitizerService htmlSanitizer,
        IStringLocalizer<SurveyDefinitionGeneralDisplayDriver> stringLocalizer)
    {
        _htmlSanitizer = htmlSanitizer;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Combine(
            Initialize<SurveyDefinitionGeneralEditViewModel>("SurveyDefinitionGeneral_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "1").Tab(S["General"], "1").Card(S["General"], "10")),
            Initialize<SurveyDefinitionGeneralEditViewModel>("SurveyDefinitionPresentation_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "2").Tab(S["General"], "1").Card(S["Presentation"], "20")),
            Initialize<SurveyDefinitionGeneralEditViewModel>("SurveyDefinitionNavigation_Edit", model => Populate(definition, model))
                .Location(l => l.Zone("Content", "3").Tab(S["General"], "1").Card(S["Navigation"], "30")));

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

    private static void Populate(SurveyDefinition definition, SurveyDefinitionGeneralEditViewModel model)
    {
        model.DefinitionTitle = definition.Title;
        model.Introduction = definition.Introduction;
        model.Presentation = definition.Presentation ?? new SurveyPresentationSettings();
        model.Navigation = definition.Navigation ?? new SurveyNavigationSettings();
    }
}
