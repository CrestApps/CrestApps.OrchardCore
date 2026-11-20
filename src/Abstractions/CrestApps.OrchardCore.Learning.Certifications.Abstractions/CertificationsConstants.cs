namespace CrestApps.OrchardCore.Learning.Certifications;

/// <summary>
/// Names shared by the Learning certification path projects.
/// </summary>
public static class CertificationsConstants
{
    /// <summary>
    /// The most credits one award or one piece of external learning may carry.
    /// </summary>
    public const decimal MaxCreditsPerAward = 1000m;

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
    /// The YesSql collection of the education credit ledger.
    /// </summary>
    public const string CreditLedgerCollectionName = "LearningCreditLedger";

    /// <summary>
    /// The recipe and deployment step of education credit types.
    /// </summary>
    public const string CreditTypesRecipeStepName = "LearningCreditTypes";

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

        /// <summary>
        /// A certificate was revoked.
        /// </summary>
        public const string Revoked = "LearningCertificateRevoked";
    }

    /// <summary>
    /// The feature ids.
    /// </summary>
    /// <summary>
    /// What awards education credits, and the kinds of ledger entries.
    /// </summary>
    public static class CreditSources
    {
        /// <summary>
        /// A completed course.
        /// </summary>
        public const string Course = "Learning.Course";

        /// <summary>
        /// A completed learning path.
        /// </summary>
        public const string LearningPath = "Learning.LearningPath";

        /// <summary>
        /// Accepted external learning.
        /// </summary>
        public const string External = "Learning.ExternalLearning";

        /// <summary>
        /// Credits a person awarded by hand or a workflow.
        /// </summary>
        public const string Manual = "Learning.Manual";

        /// <summary>
        /// A reversing post that takes back credits, such as those of an enrollment found wrong on review.
        /// </summary>
        public const string Reversal = "Learning.Reversal";
    }

    public static class Features
    {
        /// <summary>
        /// Learning certification paths.
        /// </summary>
        public const string Certifications = "CrestApps.OrchardCore.Learning.Certifications";

        /// <summary>
        /// Competencies: a course or a learning path grants Skills skills at a level on completion, and learners see
        /// the competencies they hold.
        /// </summary>
        public const string Competencies = "CrestApps.OrchardCore.Learning.Competencies";
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

        /// <summary>
        /// A number of education credits of a type earned in the cycle; the number is the requirement's minimum.
        /// </summary>
        public const string CreditsEarned = "CreditsEarned";
    }
}
