namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// The constants of Learning Assessments: the quiz course item, whose questions, attempts and grading live in a survey
/// of the Surveys module that each quiz owns, and the gradebook.
/// </summary>
public static class AssessmentsConstants
{
    /// <summary>
    /// The course item kind of quizzes.
    /// </summary>
    public const string QuizKind = "Quiz";

    /// <summary>
    /// The kind of the grading queue items of quiz responses.
    /// </summary>
    public const string QuizResponseGradingQueueKind = "QuizResponse";

    /// <summary>
    /// The recipe step of grade scales.
    /// </summary>
    public const string GradeScalesRecipeStep = "LearningGradeScales";

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The quiz course item and the grading page.
        /// </summary>
        public const string Assessments = "CrestApps.OrchardCore.Learning.Assessments";

        /// <summary>
        /// Grade scales and the gradebook.
        /// </summary>
        public const string Gradebook = "CrestApps.OrchardCore.Learning.Assessments.Gradebook";

        /// <summary>
        /// Moodle course backup import.
        /// </summary>
        public const string MoodleImport = "CrestApps.OrchardCore.Learning.Assessments.MoodleImport";
    }

    /// <summary>
    /// The content types.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// The quiz course item.
        /// </summary>
        public const string Quiz = "QuizLesson";
    }
}
