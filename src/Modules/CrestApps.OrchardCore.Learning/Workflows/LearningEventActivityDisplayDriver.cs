using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The editor of a Learning workflow event: the courses it listens to.
/// </summary>
/// <typeparam name="TActivity">The event.</typeparam>
public sealed class LearningEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, LearningEventActivityViewModel>
    where TActivity : LearningEventActivity
{
    private readonly CourseEditorOptions _editorOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="editorOptions">The course options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LearningEventActivityDisplayDriver(
        CourseEditorOptions editorOptions,
        IStringLocalizer<LearningEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        _editorOptions = editorOptions;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
    {
        return Initialize<LearningEventActivityViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Trigger"], "10"));
    }

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, LearningEventActivityViewModel model)
    {
        model.CourseIds = activity.CourseIds.ToArray();
        model.IncludeWorkflowChanges = activity.IncludeWorkflowChanges;
        model.CoursesJson = await _editorOptions.GetChosenCoursesJsonAsync(model.CourseIds);
    }

    /// <inheritdoc />
    protected override void UpdateActivity(LearningEventActivityViewModel model, TActivity activity)
    {
        activity.CourseIds = (model.CourseIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        activity.IncludeWorkflowChanges = model.IncludeWorkflowChanges;
    }
}
