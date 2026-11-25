using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Deployments;

/// <summary>
/// The editor of the enrollments deployment step: the course and the dates to export, in the editor's time zone.
/// </summary>
internal sealed class LearningEnrollmentsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningEnrollmentsDeploymentStep>
{
    private readonly CourseEditorOptions _editorOptions;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public LearningEnrollmentsDeploymentStepDisplayDriver(
        CourseEditorOptions editorOptions,
        ILocalClock localClock,
        IStringLocalizer<LearningEnrollmentsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _editorOptions = editorOptions;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningEnrollmentsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("LearningEnrollmentsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningEnrollmentsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(LearningEnrollmentsDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<LearningEnrollmentsDeploymentStepViewModel>("LearningEnrollmentsDeploymentStep_Fields_Edit", async model =>
        {
            model.CourseId = step.CourseId;
            model.IncludeProgress = step.IncludeProgress;
            model.From = step.FromUtc is { } from ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(from, DateTimeKind.Utc))).DateTime : null;
            model.To = step.ToUtc is { } to ? (await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(to, DateTimeKind.Utc))).DateTime : null;
            model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(null, [step.CourseId]);
        }).Location(l => l.Zone("Content", "1").Card(S["Learner Records"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(LearningEnrollmentsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new LearningEnrollmentsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        step.CourseId = string.IsNullOrWhiteSpace(model.CourseId) ? null : model.CourseId;
        step.IncludeProgress = model.IncludeProgress;
        step.FromUtc = model.From is { } from ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(from, DateTimeKind.Unspecified)) : null;
        step.ToUtc = model.To is { } to ? await _localClock.ToUtcLenientAsync(DateTime.SpecifyKind(to, DateTimeKind.Unspecified)) : null;

        return Edit(step, context);
    }
}
