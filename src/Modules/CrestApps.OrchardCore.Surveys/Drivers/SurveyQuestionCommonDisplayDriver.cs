using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits what every question has: text, code, description, required, visibility and the required and enabled rules.
/// </summary>
internal sealed class SurveyQuestionCommonDisplayDriver : DisplayDriver<SurveyQuestion>
{
    private readonly SurveyDesignerContext _designer;
    private readonly ISurveyControlRegistry _registry;
    private readonly IHtmlSanitizerService _htmlSanitizer;

    internal readonly IStringLocalizer S;

    public SurveyQuestionCommonDisplayDriver(
        SurveyDesignerContext designer,
        ISurveyControlRegistry registry,
        IHtmlSanitizerService htmlSanitizer,
        IStringLocalizer<SurveyQuestionCommonDisplayDriver> stringLocalizer)
    {
        _designer = designer;
        _registry = registry;
        _htmlSanitizer = htmlSanitizer;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion question, BuildEditorContext context)
        => Initialize<SurveyQuestionCommonEditViewModel>("SurveyQuestionCommon_Edit", model =>
        {
            var definition = _designer.Definition;
            var (page, pageIndex) = FindPage(definition, question.Id);
            var descriptor = _registry.GetControl(question.ControlType);

            model.Title = question.Title;
            model.Name = question.Name;
            model.Description = question.Description;
            model.Placeholder = question.Placeholder;
            model.Required = question.Required;
            model.Pinned = question.Pinned;
            model.VisibilityMode = question.Visibility?.Mode ?? VisibilityMode.Always;
            model.VisibilityRule = SurveyRuleJson.Write(question.Visibility?.Rule);
            model.VisibilityFormula = question.Visibility?.Formula;
            model.RequiredWhen = SurveyRuleJson.Write(question.RequiredWhen);
            model.EnabledWhen = SurveyRuleJson.Write(question.EnabledWhen);
            model.PoolId = question.PoolId;
            model.Answerable = descriptor?.Has(SurveyControlCapabilities.Answerable) ?? true;
            model.Sources = SurveyRuleSources.Build(definition, _registry, pageIndex < 0 ? int.MaxValue : pageIndex, question.Id);
            model.Pools = (page?.Pools ?? [])
                .Select(pool => new SelectListItem(pool.Title ?? pool.Name, pool.Id, pool.Id == question.PoolId))
                .ToList();
        }).Location("Content:1");

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion question, UpdateEditorContext context)
    {
        var model = new SurveyQuestionCommonEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!SurveyNames.IsValidVariableCode(model.Name?.Trim()))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The code needs letters, digits and underscores, and must start with a letter."]);
        }
        else
        {
            question.Name = model.Name.Trim();
        }

        question.Title = model.Title?.Trim();
        question.Description = string.IsNullOrWhiteSpace(model.Description) ? null : _htmlSanitizer.Sanitize(model.Description);
        question.Placeholder = model.Placeholder?.Trim();
        question.Required = model.Required;
        question.Pinned = model.Pinned;
        question.Visibility = new SurveyVisibility
        {
            Mode = model.VisibilityMode,
            Rule = model.VisibilityMode == VisibilityMode.Always ? null : SurveyRuleJson.Read(model.VisibilityRule),
            Formula = model.VisibilityMode == VisibilityMode.Always || string.IsNullOrWhiteSpace(model.VisibilityFormula) ? null : model.VisibilityFormula.Trim(),
        };
        question.RequiredWhen = SurveyRuleJson.Read(model.RequiredWhen);
        question.EnabledWhen = SurveyRuleJson.Read(model.EnabledWhen);
        question.PoolId = string.IsNullOrEmpty(model.PoolId) ? null : model.PoolId;

        return Edit(question, context);
    }

    private static (SurveyPage Page, int Index) FindPage(SurveyDefinition definition, string questionId)
    {
        if (definition is null)
        {
            return (null, -1);
        }

        for (var i = 0; i < definition.Pages.Count; i++)
        {
            if (definition.Pages[i].Questions.Any(question => question.Id == questionId))
            {
                return (definition.Pages[i], i);
            }
        }

        return (null, -1);
    }
}
