using CrestApps.OrchardCore.Learning.Services;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="editorOptions">The course options.</param>
    public LearningEventActivityDisplayDriver(CourseEditorOptions editorOptions)
    {
        _editorOptions = editorOptions;
    }

    /// <inheritdoc />
    protected override async ValueTask EditActivityAsync(TActivity activity, LearningEventActivityViewModel model)
    {
        model.CourseIds = activity.CourseIds.ToArray();
        model.IncludeWorkflowChanges = activity.IncludeWorkflowChanges;
        model.CourseOptions = await _editorOptions.GetCourseOptionsAsync(null, model.CourseIds);
    }

    /// <inheritdoc />
    protected override void UpdateActivity(LearningEventActivityViewModel model, TActivity activity)
    {
        activity.CourseIds = (model.CourseIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        activity.IncludeWorkflowChanges = model.IncludeWorkflowChanges;
    }
}
