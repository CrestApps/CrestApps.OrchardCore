using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the choice list of question types that use choices: texts, values, images, other and exclusive choices, pinned
/// and retired choices, shuffling, choice visibility and carried-forward choices.
/// </summary>
internal sealed class SurveyChoicesDisplayDriver : DisplayDriver<SurveyQuestion>
{
    private readonly SurveyDesignerContext _designer;
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    public SurveyChoicesDisplayDriver(
        SurveyDesignerContext designer,
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyChoicesDisplayDriver> stringLocalizer)
    {
        _designer = designer;
        _registry = registry;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => _registry.GetControl(model?.ControlType)?.Has(SurveyControlCapabilities.UsesChoices) == true;

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion question, BuildEditorContext context)
        => Initialize<SurveyChoicesEditViewModel>("SurveyChoices_Edit", model =>
        {
            var definition = _designer.Definition;
            var pageIndex = PageIndex(definition, question.Id);

            model.Choices = question.Choices.ToList();
            model.ChoiceRules = question.Choices.Select(choice => SurveyRuleJson.Write(choice.Visibility?.Rule)).ToList();
            model.ShuffleChoices = question.ShuffleChoices;
            model.ChoicesFrom = question.ChoicesFrom;
            model.ChoicesFromMode = question.ChoicesFromMode;
            model.SupportsOther = _registry.GetControl(question.ControlType)?.Has(SurveyControlCapabilities.SupportsOther) == true;
            model.Sources = SurveyRuleSources.Build(definition, _registry, pageIndex, question.Id);
            model.CarryForwardSources = SurveyDefinitions.Questions(definition)
                .Where(item => item.PageIndex < pageIndex && item.Question.Choices.Count > 0 && item.Question.Id != question.Id)
                .Select(item => new SelectListItem($"{item.Question.Name}: {item.Question.Title}", item.Question.Id, item.Question.Id == question.ChoicesFrom))
                .ToList();
        }).Location("Content:2");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion question, UpdateEditorContext context)
    {
        var model = new SurveyChoicesEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var choices = new List<SurveyChoice>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < model.Choices.Count; i++)
        {
            var choice = model.Choices[i];

            if (choice is null || (string.IsNullOrWhiteSpace(choice.Text) && string.IsNullOrWhiteSpace(choice.ImageUrl)))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(choice.Id) || !ids.Add(choice.Id))
            {
                choice.Id = IdGenerator.GenerateId();
                ids.Add(choice.Id);
            }

            var rule = i < model.ChoiceRules.Count ? SurveyRuleJson.Read(model.ChoiceRules[i]) : null;

            choice.Text = choice.Text?.Trim();
            choice.Value = string.IsNullOrWhiteSpace(choice.Value) ? (choices.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : choice.Value.Trim();
            choice.Visibility = rule is null ? new SurveyVisibility() : new SurveyVisibility { Mode = VisibilityMode.ShowWhen, Rule = rule };
            choices.Add(choice);
        }

        var exclusive = choices.Count(choice => choice.IsExclusive);

        if (choices.Count(choice => choice.IsOther) > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Choices), S["A question can have one other choice."]);
        }

        if (exclusive > 0 && exclusive == choices.Count)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Choices), S["At least one choice must not clear the others."]);
        }

        question.Choices = choices;
        question.ShuffleChoices = model.ShuffleChoices;
        question.ChoicesFrom = string.IsNullOrEmpty(model.ChoicesFrom) ? null : model.ChoicesFrom;
        question.ChoicesFromMode = model.ChoicesFromMode;

        return Edit(question, context);
    }

    private static int PageIndex(SurveyDefinition definition, string questionId)
        => SurveyDefinitions.Questions(definition).Where(item => item.Question.Id == questionId).Select(item => item.PageIndex).DefaultIfEmpty(int.MaxValue).First();
}
