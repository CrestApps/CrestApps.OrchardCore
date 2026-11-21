namespace CrestApps.OrchardCore.Learning.AI;

/// <summary>
/// The names Learning AI uses: feature ids, AI tool names and template ids.
/// </summary>
public static class LearningAIConstants
{
    /// <summary>
    /// The usage purpose that the learning assistant's own model calls are metered under.
    /// </summary>
    public const string UsagePurpose = "Learning";

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The learning assistant: the tutor template, the Learning AI tools, the enrollment retrieval filter and the
        /// course switch.
        /// </summary>
        public const string Assistant = "CrestApps.OrchardCore.Learning.AI";

        /// <summary>
        /// The authoring assistant: draft outlines, questions, rubrics and translations, and scoring help for graders.
        /// </summary>
        public const string Authoring = "CrestApps.OrchardCore.Learning.AI.Authoring";
    }

    /// <summary>
    /// The names of the Learning AI tools.
    /// </summary>
    public static class Tools
    {
        /// <summary>
        /// The learner's own enrollments with their progress and dates.
        /// </summary>
        public const string MyProgress = "getMyLearningProgress";

        /// <summary>
        /// The next lesson the learner can open.
        /// </summary>
        public const string MyNextLesson = "getMyNextLesson";

        /// <summary>
        /// The learner's result of a quiz, explained only as far as its review policy allows.
        /// </summary>
        public const string ExplainMyResult = "explainMyQuizResult";

        /// <summary>
        /// The learners whose course is overdue, within the caller's scope.
        /// </summary>
        public const string OverdueLearners = "listOverdueLearners";

        /// <summary>
        /// The learners whose access to a course ends soon, within the caller's scope.
        /// </summary>
        public const string ExpiringLearners = "listLearnersExpiringSoon";
    }

    /// <summary>
    /// The AI templates Learning AI ships, by the id they are discovered under.
    /// </summary>
    public static class Templates
    {
        /// <summary>
        /// The tutor AI profile template.
        /// </summary>
        public const string Tutor = "learning-tutor";

        /// <summary>
        /// Drafts a course outline.
        /// </summary>
        public const string DraftOutline = "learning-draft-outline";

        /// <summary>
        /// Drafts questions from a lesson.
        /// </summary>
        public const string DraftQuestions = "learning-draft-questions";

        /// <summary>
        /// Drafts a rubric.
        /// </summary>
        public const string DraftRubric = "learning-draft-rubric";

        /// <summary>
        /// Translates a lesson.
        /// </summary>
        public const string Translate = "learning-translate";

        /// <summary>
        /// Suggests a score and feedback for an essay or a text submission.
        /// </summary>
        public const string ScoreEssay = "learning-score-essay";
    }
}
