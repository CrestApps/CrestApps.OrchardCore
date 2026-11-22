using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the endings of a survey definition.
/// </summary>
internal sealed class SurveyDefinitionEndingsDisplayDriver : DisplayDriver<SurveyDefinition>
{
    private readonly ISurveyControlRegistry _registry;
    private readonly IHtmlSanitizerService _htmlSanitizer;

    internal readonly IStringLocalizer S;

    public SurveyDefinitionEndingsDisplayDriver(
        ISurveyControlRegistry registry,
        IHtmlSanitizerService htmlSanitizer,
        IStringLocalizer<SurveyDefinitionEndingsDisplayDriver> stringLocalizer)
    {
        _registry = registry;
        _htmlSanitizer = htmlSanitizer;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyDefinition definition, BuildEditorContext context)
        => Initialize<SurveyEndingsEditViewModel>("SurveyDefinitionEndings_Edit", model =>
        {
            model.Endings = definition.Endings.ToList();
            model.EndingRules = definition.Endings.Select(ending => SurveyRuleJson.Write(ending.Condition?.Rule)).ToList();
            model.DefaultEndingId = definition.Endings.FirstOrDefault(ending => ending.IsDefault)?.Id;
            model.Sources = SurveyRuleSources.Build(definition, _registry, int.MaxValue);
        }).Location("Content:1#Endings;3");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyDefinition definition, UpdateEditorContext context)
    {
        var model = new SurveyEndingsEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var endings = new List<SurveyEnding>();

        for (var i = 0; i < model.Endings.Count; i++)
        {
            var ending = model.Endings[i];

            if (ending is null || (string.IsNullOrWhiteSpace(ending.Title) && string.IsNullOrWhiteSpace(ending.Message) && string.IsNullOrWhiteSpace(ending.RedirectUrl)))
            {
                continue;
            }

            ending.Id = string.IsNullOrWhiteSpace(ending.Id) ? IdGenerator.GenerateId() : ending.Id;
            ending.Name = SurveyNames.IsValidVariableCode(ending.Name?.Trim()) ? ending.Name.Trim() : $"ending_{endings.Count + 1}";
            ending.Message = string.IsNullOrWhiteSpace(ending.Message) ? null : _htmlSanitizer.Sanitize(ending.Message);
            ending.RedirectUrl = string.IsNullOrWhiteSpace(ending.RedirectUrl) ? null : ending.RedirectUrl.Trim();

            var rule = i < model.EndingRules.Count ? SurveyRuleJson.Read(model.EndingRules[i]) : null;
            ending.Condition = rule is null ? new SurveyVisibility() : new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Rule = rule };
            endings.Add(ending);
        }

        if (endings.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endings), S["The survey needs at least one ending."]);
        }

        var defaultId = endings.Any(ending => ending.Id == model.DefaultEndingId) ? model.DefaultEndingId : endings.FirstOrDefault()?.Id;

        foreach (var ending in endings)
        {
            ending.IsDefault = ending.Id == defaultId;
        }

        if (endings.Count > 0)
        {
            definition.Endings = endings;
        }

        return Edit(definition, context);
    }
}
