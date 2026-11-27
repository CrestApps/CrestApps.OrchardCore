using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Certifications.Pipelines;

/// <summary>
/// The fields of the Record external learning step.
/// </summary>
public class RecordExternalLearningStepViewModel
{
    public string UserIdField { get; set; }

    public string TitleField { get; set; }

    public string ProviderField { get; set; }

    public string AmountField { get; set; }

    public string CompletedOnField { get; set; }

    public string CreditTypeId { get; set; }

    public string CreditTypeField { get; set; }

    [BindNever]
    public IList<SelectListItem> CreditTypeOptions { get; set; } = [];
}

/// <summary>
/// The editor of the Record external learning step.
/// </summary>
internal sealed class RecordExternalLearningStepDisplayDriver : DisplayDriver<DataPipelineStep>
{
    private readonly IEducationCreditService _credits;

    internal readonly IStringLocalizer S;

    public RecordExternalLearningStepDisplayDriver(IEducationCreditService credits, IStringLocalizer<RecordExternalLearningStepDisplayDriver> stringLocalizer)
    {
        _credits = credits;
        S = stringLocalizer;
    }

    public override bool CanHandleModel(DataPipelineStep model)
        => model?.Type == RecordExternalLearningStep.StepName;

    public override IDisplayResult Display(DataPipelineStep step, BuildDisplayContext context)
        => Initialize<RecordExternalLearningStepViewModel>($"{RecordExternalLearningStep.StepName}_Fields_Design", model => FillAsync(step, model)).Location("Design", "Content");

    public override IDisplayResult Edit(DataPipelineStep step, BuildEditorContext context)
        => Combine(
            Initialize<RecordExternalLearningStepViewModel>($"{RecordExternalLearningStep.StepName}_Fields_Edit", model => FillAsync(step, model))
                .Location(l => l.Zone("Content", "1").Card(S["Learning"], "10")),
            Initialize<RecordExternalLearningStepViewModel>($"{RecordExternalLearningStep.StepName}CreditType_Fields_Edit", model => FillAsync(step, model))
                .Location(l => l.Zone("Content", "2").Card(S["Credit Type"], "20")));

    public override async Task<IDisplayResult> UpdateAsync(DataPipelineStep step, UpdateEditorContext context)
    {
        var model = new RecordExternalLearningStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        foreach (var (value, name, message) in new[]
        {
            (model.UserIdField, nameof(model.UserIdField), S["Give the field that holds the user id."].Value),
            (model.TitleField, nameof(model.TitleField), S["Give the field that holds what the learning was."].Value),
            (model.AmountField, nameof(model.AmountField), S["Give the field that holds the credits."].Value),
            (model.CompletedOnField, nameof(model.CompletedOnField), S["Give the field that holds the completion date."].Value),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + name, message);
            }
        }

        if (string.IsNullOrWhiteSpace(model.CreditTypeId) && string.IsNullOrWhiteSpace(model.CreditTypeField))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CreditTypeId), S["Choose the credit type, or give the field that holds it."]);
        }

        RecordExternalLearningStep.SetSettings(step, new RecordExternalLearningStepSettings
        {
            UserIdField = model.UserIdField?.Trim(),
            TitleField = model.TitleField?.Trim(),
            ProviderField = string.IsNullOrWhiteSpace(model.ProviderField) ? null : model.ProviderField.Trim(),
            AmountField = model.AmountField?.Trim(),
            CompletedOnField = model.CompletedOnField?.Trim(),
            CreditTypeId = string.IsNullOrWhiteSpace(model.CreditTypeId) ? null : model.CreditTypeId,
            CreditTypeField = string.IsNullOrWhiteSpace(model.CreditTypeField) ? null : model.CreditTypeField.Trim(),
        });

        return Edit(step, context);
    }

    private async ValueTask FillAsync(DataPipelineStep step, RecordExternalLearningStepViewModel model)
    {
        var settings = RecordExternalLearningStep.GetSettings(step);

        model.UserIdField = settings.UserIdField;
        model.TitleField = settings.TitleField;
        model.ProviderField = settings.ProviderField;
        model.AmountField = settings.AmountField;
        model.CompletedOnField = settings.CompletedOnField;
        model.CreditTypeId = settings.CreditTypeId;
        model.CreditTypeField = settings.CreditTypeField;
        model.CreditTypeOptions = (await _credits.ListTypesAsync()).Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == settings.CreditTypeId)).ToList();
    }
}
