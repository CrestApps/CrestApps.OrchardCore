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
    /// <summary>
    /// The longest feedback a grader can give, on the page and on the server.
    /// </summary>
    public const int MaxFeedbackLength = 10_000;

    /// <summary>
    /// The longest reason for grading graded work again, on the page and on the server.
    /// </summary>
    public const int MaxReasonLength = 500;

    /// <summary>
    /// The notification types of coursework notices to learners.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// A submission was graded, or graded again.
        /// </summary>
        public const string Graded = "LearningCourseworkGraded";

        /// <summary>
        /// A submission was returned for the learner to submit again.
        /// </summary>
        public const string Returned = "LearningCourseworkReturned";
    }

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
