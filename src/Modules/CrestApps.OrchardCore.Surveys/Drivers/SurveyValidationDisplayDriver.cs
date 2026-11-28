using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the validation rules of question types that support them.
/// </summary>
internal sealed class SurveyValidationDisplayDriver : DisplayDriver<SurveyQuestion>
{
    private readonly ISurveyControlRegistry _registry;

    internal readonly IStringLocalizer S;

    public SurveyValidationDisplayDriver(
        ISurveyControlRegistry registry,
        IStringLocalizer<SurveyValidationDisplayDriver> stringLocalizer)
    {
        _registry = registry;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override bool CanHandleModel(SurveyQuestion model)
        => _registry.GetControl(model?.ControlType)?.Has(SurveyControlCapabilities.SupportsValidation) == true;

    /// <inheritdoc />
    public override IDisplayResult Edit(SurveyQuestion question, BuildEditorContext context)
        => Initialize<SurveyValidationEditViewModel>("SurveyValidation_Edit", model => model.ValidationRules = question.Validation.ToList())
            .Location(l => l.Zone("Content", "6").Card(S["Validation"], "60"));

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(SurveyQuestion question, UpdateEditorContext context)
    {
        var model = new SurveyValidationEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        question.Validation = model.ValidationRules
            .Where(rule => rule is not null && (!string.IsNullOrWhiteSpace(rule.Value) || rule.Kind is SurveyValidationKind.Email or SurveyValidationKind.Url))
            .Select(rule => new SurveyValidationRule
            {
                Kind = rule.Kind,
                Value = rule.Value?.Trim(),
                Message = string.IsNullOrWhiteSpace(rule.Message) ? null : rule.Message.Trim(),
            })
            .ToList();

        return Edit(question, context);
    }
}
