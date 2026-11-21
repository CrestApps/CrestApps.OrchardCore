namespace CrestApps.OrchardCore.Learning.Programs;

/// <summary>
/// The names Learning programs uses.
/// </summary>
public static class ProgramsConstants
{
    /// <summary>
    /// The YesSql collection of path progress, required learning and assignments.
    /// </summary>
    public const string CollectionName = "LearningPrograms";

    /// <summary>
    /// The grant source of an enrollment a learning path made.
    /// </summary>
    public const string PathSource = "Learning.LearningPath";

    /// <summary>
    /// The grant source of an enrollment or path progress that required learning made.
    /// </summary>
    public const string RequirementSource = "Learning.RequiredLearning";

    /// <summary>
    /// The content type of a learning path.
    /// </summary>
    public const string LearningPathContentType = "LearningPath";

    /// <summary>
    /// The recipe and deployment step of required learning.
    /// </summary>
    public const string RecipeStepName = "LearningRequirements";

    /// <summary>
    /// The data source name.
    /// </summary>
    public const string DataSourceName = "LearningPrograms";

    /// <summary>
    /// The features of the module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Learning paths.
        /// </summary>
        public const string Paths = "CrestApps.OrchardCore.Learning.Programs";

        /// <summary>
        /// Required learning.
        /// </summary>
        public const string RequiredLearning = "CrestApps.OrchardCore.Learning.Programs.RequiredLearning";

        /// <summary>
        /// Team learning.
        /// </summary>
        public const string TeamLearning = "CrestApps.OrchardCore.Learning.Programs.TeamLearning";
    }

    /// <summary>
    /// The kinds of target required learning can require.
    /// </summary>
    public static class TargetKinds
    {
        /// <summary>
        /// A course.
        /// </summary>
        public const string Course = "Course";

        /// <summary>
        /// A learning path.
        /// </summary>
        public const string LearningPath = "LearningPath";

        /// <summary>
        /// A certification path.
        /// </summary>
        public const string CertificationPath = "CertificationPath";
    }

    /// <summary>
    /// The notification types.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// Learning was required of the person.
        /// </summary>
        public const string Required = "LearningRequired";

        /// <summary>
        /// Required learning is due soon.
        /// </summary>
        public const string DueSoon = "LearningRequiredDueSoon";

        /// <summary>
        /// Required learning of a report is overdue.
        /// </summary>
        public const string Overdue = "LearningRequiredOverdue";

        /// <summary>
        /// A learning path was completed.
        /// </summary>
        public const string PathCompleted = "LearningPathCompleted";
    }
}
