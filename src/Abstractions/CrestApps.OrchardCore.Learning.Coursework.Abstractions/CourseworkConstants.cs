namespace CrestApps.OrchardCore.Learning.Coursework;

/// <summary>
/// The names Learning coursework uses.
/// </summary>
public static class CourseworkConstants
{
    /// <summary>
    /// The feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Learning.Coursework";

    /// <summary>
    /// The course item kind of coursework.
    /// </summary>
    public const string CourseworkKind = "Coursework";

    /// <summary>
    /// The YesSql collection of submissions.
    /// </summary>
    public const string CollectionName = "LearningSubmissions";

    /// <summary>
    /// The name of the submissions recipe and deployment step.
    /// </summary>
    public const string SubmissionsRecipeStep = "LearningSubmissions";

    /// <summary>
    /// The name of the coursework data source of Data Pipelines and the Report Builder.
    /// </summary>
    public const string DataSourceName = "LearningCoursework";

    /// <summary>
    /// The kind of a submission in the grading list.
    /// </summary>
    public const string GradingQueueKind = "Submission";

    /// <summary>
    /// The folder of the protected file area of submitted files, in the tenant's App_Data.
    /// </summary>
    public const string FilesFolder = "LearningCoursework";

    /// <summary>
    /// The content types Learning coursework adds.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// Coursework in a course outline.
        /// </summary>
        public const string Coursework = "CourseworkLesson";

        /// <summary>
        /// A rubric graders score coursework against.
        /// </summary>
        public const string Rubric = "Rubric";
    }
}
