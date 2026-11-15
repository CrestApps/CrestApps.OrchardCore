namespace CrestApps.OrchardCore.Learning.Certifications;

/// <summary>
/// Names shared by the Learning certification path projects.
/// </summary>
public static class CertificationsConstants
{
    /// <summary>
    /// The YesSql collection of paths and progress.
    /// </summary>
    public const string CollectionName = "LearningCertifications";

    /// <summary>
    /// The YesSql collection of the renewal work queue.
    /// </summary>
    public const string RenewalWorkCollectionName = "LearningCertificationRenewals";

    /// <summary>
    /// The source Learning issues Skills certificates and enrollments under.
    /// </summary>
    public const string Source = "Learning.CertificationPath";

    /// <summary>
    /// The name of the recipe and deployment step.
    /// </summary>
    public const string RecipeStepName = "LearningCertificationPaths";

    /// <summary>
    /// The name of the data source.
    /// </summary>
    public const string DataSourceName = "LearningCertifications";

    /// <summary>
    /// The notification types.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// A certificate was issued or renewed.
        /// </summary>
        public const string Issued = "LearningCertificateIssued";

        /// <summary>
        /// Renewal opened.
        /// </summary>
        public const string RenewalOpened = "LearningCertificationRenewalOpened";

        /// <summary>
        /// A certificate expires soon.
        /// </summary>
        public const string Expiring = "LearningCertificateExpiring";

        /// <summary>
        /// A certificate lapsed.
        /// </summary>
        public const string Lapsed = "LearningCertificationLapsed";
    }

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Learning certification paths.
        /// </summary>
        public const string Certifications = "CrestApps.OrchardCore.Learning.Certifications";
    }

    /// <summary>
    /// The requirement kinds Learning brings.
    /// </summary>
    public static class RequirementKinds
    {
        /// <summary>
        /// A course completed, with a minimum grade when one is set.
        /// </summary>
        public const string CourseCompleted = "CourseCompleted";

        /// <summary>
        /// A quiz passed, with a minimum percent when one is set.
        /// </summary>
        public const string AssessmentPassed = "AssessmentPassed";
    }
}
