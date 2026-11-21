using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DataPipelines.Models;
using OrchardCore.DataPipelines.Steps;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Pipelines;

/// <summary>
/// The designer editor of the Enroll learners step.
/// </summary>
internal sealed class EnrollLearnersStepDisplayDriver : DisplayDriver<DataPipelineStep>
{
    private readonly CourseEditorOptions _editorOptions;

    internal readonly IStringLocalizer S;

    public EnrollLearnersStepDisplayDriver(
        CourseEditorOptions editorOptions,
        IStringLocalizer<EnrollLearnersStepDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        S = stringLocalizer;
    }

    public override bool CanHandleModel(DataPipelineStep model)
        => model?.Type == EnrollLearnersStep.StepName;

    public override IDisplayResult Display(DataPipelineStep step, BuildDisplayContext context)
    {
        return Initialize<EnrollLearnersStepViewModel>($"{EnrollLearnersStep.StepName}_Fields_Design", model => FillAsync(step, model))
            .Location("Design", "Content");
    }

    public override IDisplayResult Edit(DataPipelineStep step, BuildEditorContext context)
    {
        return Initialize<EnrollLearnersStepViewModel>($"{EnrollLearnersStep.StepName}_Fields_Edit", model => FillAsync(step, model))
            .Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataPipelineStep step, UpdateEditorContext context)
    {
        var model = new EnrollLearnersStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.UserIdField))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.UserIdField), S["Give the field that holds the user id of the learner."]);
        }

        if (string.IsNullOrWhiteSpace(model.CourseId) && string.IsNullOrWhiteSpace(model.CourseIdField))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.CourseId), S["Choose the course, or give the field that holds it."]);
        }

        EnrollLearnersStep.SetSettings(step, new EnrollLearnersStepSettings
        {
            UserIdField = model.UserIdField?.Trim(),
            CourseId = string.IsNullOrWhiteSpace(model.CourseId) ? null : model.CourseId,
            CourseIdField = string.IsNullOrWhiteSpace(model.CourseIdField) ? null : model.CourseIdField.Trim(),
            DueField = string.IsNullOrWhiteSpace(model.DueField) ? null : model.DueField.Trim(),
            AccessEndsField = string.IsNullOrWhiteSpace(model.AccessEndsField) ? null : model.AccessEndsField.Trim(),
            IgnorePrerequisites = model.IgnorePrerequisites,
        });

        return Edit(step, context);
    }

    private async ValueTask FillAsync(DataPipelineStep step, EnrollLearnersStepViewModel model)
    {
        var settings = EnrollLearnersStep.GetSettings(step);

        model.UserIdField = settings.UserIdField;
        model.CourseId = settings.CourseId;
        model.CourseIdField = settings.CourseIdField;
        model.DueField = settings.DueField;
        model.AccessEndsField = settings.AccessEndsField;
        model.IgnorePrerequisites = settings.IgnorePrerequisites;
        model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(null, [settings.CourseId]);
        model.CourseTitle = model.CourseOptions.FirstOrDefault(option => option.Value == settings.CourseId)?.Text;
    }
}
