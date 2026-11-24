namespace CrestApps.OrchardCore.Learning;

/// <summary>
/// The names the Learning modules share: feature ids, content types and parts, the document collection, enrollment
/// sources, course item kinds, completion modes and the names stored in recipes, reports and notifications.
/// </summary>
public static class LearningConstants
{
    /// <summary>
    /// The YesSql collection that holds the learner records of Learning.
    /// </summary>
    public const string CollectionName = "Learning";

    /// <summary>
    /// The number series of enrollment numbers.
    /// </summary>
    public const string EnrollmentNumberSeries = "LearningEnrollment";

    /// <summary>
    /// The name of the recipe and deployment step that moves enrollments with their progress.
    /// </summary>
    public const string EnrollmentsRecipeStep = "LearningEnrollments";

    /// <summary>
    /// The name of the Learning data source of Data Pipelines and the Report Builder.
    /// </summary>
    public const string DataSourceName = "Learning";

    /// <summary>
    /// The Audit Trail category of Learning.
    /// </summary>
    public const string AuditTrailCategory = "Learning";

    /// <summary>
    /// The staff capability Learning registers when Staff is on.
    /// </summary>
    public const string InstructorCapability = "Instructor";

    /// <summary>
    /// The feature ids of the Learning module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Courses, course items, enrollment, progress and completion.
        /// </summary>
        public const string Learning = "CrestApps.OrchardCore.Learning";

        /// <summary>
        /// The pages a learner uses: the course page, the player, My learning and the transcript.
        /// </summary>
        public const string LearnerPages = "CrestApps.OrchardCore.Learning.LearnerPages";

        /// <summary>
        /// Lets a learner enroll in an open course, with or without an enrollment key.
        /// </summary>
        public const string SelfEnrollment = "CrestApps.OrchardCore.Learning.SelfEnrollment";

        /// <summary>
        /// Enrollment by cohort: a course or a learning path linked to cohorts enrolls the people who join them and
        /// ends the enrollments it made for the people who leave.
        /// </summary>
        public const string Cohorts = "CrestApps.OrchardCore.Learning.Cohorts";

        /// <summary>
        /// A tree of course categories from an Orchard Core taxonomy: a category field on courses, category browsing in
        /// the course catalog and a category filter in the admin list of courses.
        /// </summary>
        public const string CourseCategories = "CrestApps.OrchardCore.Learning.CourseCategories";
    }

    /// <summary>
    /// The names of the course categories: the taxonomy, its term type and the category field of the Course type.
    /// </summary>
    public static class CourseCategories
    {
        /// <summary>
        /// The title of the taxonomy the Course Categories feature creates when the site has none.
        /// </summary>
        public const string TaxonomyName = "Course categories";

        /// <summary>
        /// The alias Orchard Core gives a taxonomy titled <see cref="TaxonomyName"/>.
        /// </summary>
        public const string TaxonomyAlias = "course-categories";

        /// <summary>
        /// The content type of the taxonomy's terms, when the feature creates the taxonomy.
        /// </summary>
        public const string TermContentType = "CourseCategory";

        /// <summary>
        /// The name of the taxonomy field on the Course type's own part.
        /// </summary>
        public const string FieldName = "Category";
    }

    /// <summary>
    /// The content types Learning creates.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A course.
        /// </summary>
        public const string Course = "Course";

        /// <summary>
        /// A page lesson.
        /// </summary>
        public const string PageLesson = "PageLesson";

        /// <summary>
        /// A video lesson.
        /// </summary>
        public const string VideoLesson = "VideoLesson";

        /// <summary>
        /// A file lesson.
        /// </summary>
        public const string FileLesson = "FileLesson";

        /// <summary>
        /// A link lesson.
        /// </summary>
        public const string LinkLesson = "LinkLesson";

        /// <summary>
        /// A policy the learner confirms they have read and understood.
        /// </summary>
        public const string Acknowledgement = "Acknowledgement";

        /// <summary>
        /// A block of text, pictures and video shown in place in a course.
        /// </summary>
        public const string TextBlock = "TextBlock";
    }

    /// <summary>
    /// The content parts Learning defines.
    /// </summary>
    public static class ContentParts
    {
        /// <summary>
        /// Marks a content type as a course.
        /// </summary>
        public const string Course = "CoursePart";

        /// <summary>
        /// The ordered sections of a course.
        /// </summary>
        public const string CourseOutline = "CourseOutlinePart";

        /// <summary>
        /// Marks a content type as a course item.
        /// </summary>
        public const string CourseItem = "CourseItemPart";

        /// <summary>
        /// The player settings of a video lesson.
        /// </summary>
        public const string VideoLesson = "VideoLessonPart";

        /// <summary>
        /// The statement of an acknowledgement.
        /// </summary>
        public const string Acknowledgement = "AcknowledgementPart";
    }

    /// <summary>
    /// The built-in enrollment sources. Other modules register their own with <c>AddEnrollmentSource</c>.
    /// </summary>
    public static class EnrollmentSources
    {
        /// <summary>
        /// A person with the permission enrolled the learner.
        /// </summary>
        public const string Manual = "Manual";

        /// <summary>
        /// The learner enrolled in an open course.
        /// </summary>
        public const string Self = "Self";

        /// <summary>
        /// The learner enrolled with the course's enrollment key.
        /// </summary>
        public const string Key = "Key";

        /// <summary>
        /// A data pipeline enrolled the learner.
        /// </summary>
        public const string Pipeline = "Pipeline";

        /// <summary>
        /// A workflow enrolled the learner.
        /// </summary>
        public const string Workflow = "Workflow";

        /// <summary>
        /// The learner is in a cohort the course or learning path is linked to.
        /// </summary>
        public const string Cohort = "Cohort";
    }

    /// <summary>
    /// The course item kinds Learning registers.
    /// </summary>
    public static class CourseItemKinds
    {
        /// <summary>
        /// A page of text with attached files.
        /// </summary>
        public const string Page = "Page";

        /// <summary>
        /// A video.
        /// </summary>
        public const string Video = "Video";

        /// <summary>
        /// A file to read or download.
        /// </summary>
        public const string File = "File";

        /// <summary>
        /// A link to a page elsewhere.
        /// </summary>
        public const string Link = "Link";

        /// <summary>
        /// A statement the learner confirms.
        /// </summary>
        public const string Acknowledgement = "Acknowledgement";

        /// <summary>
        /// Text, pictures and video shown in place in the outline and the player, never tracked for completion (a
        /// Moodle label).
        /// </summary>
        public const string Text = "Text";
    }

    /// <summary>
    /// The roles a person can hold on the staff of a course. The value is an open string.
    /// </summary>
    public static class CourseStaffRoles
    {
        /// <summary>
        /// Teaches the course and has the instructor's rights on it.
        /// </summary>
        public const string Instructor = "Instructor";

        /// <summary>
        /// Helps the instructor and has the instructor's rights on it.
        /// </summary>
        public const string Assistant = "Assistant";

        /// <summary>
        /// Grades work in the course.
        /// </summary>
        public const string Grader = "Grader";

        /// <summary>
        /// Sees the learners' records of the course without changing them.
        /// </summary>
        public const string Observer = "Observer";
    }

    /// <summary>
    /// The notification types of Learning.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// The person was enrolled in a course.
        /// </summary>
        public const string Enrolled = "LearningEnrolled";

        /// <summary>
        /// A request to enroll in a course that needs approval was declined. An approved request is told with
        /// <see cref="Enrolled"/>.
        /// </summary>
        public const string EnrollmentDeclined = "LearningEnrollmentDeclined";

        /// <summary>
        /// A course is due soon.
        /// </summary>
        public const string DueSoon = "LearningDueSoon";

        /// <summary>
        /// A course is overdue.
        /// </summary>
        public const string Overdue = "LearningOverdue";

        /// <summary>
        /// A completed enrollment in a course the person teaches needs review.
        /// </summary>
        public const string ReviewNeeded = "LearningReviewNeeded";

        /// <summary>
        /// Someone asked to enroll in a course that needs approval, which the person may approve.
        /// </summary>
        public const string EnrollmentRequested = "LearningEnrollmentRequested";
    }
}
