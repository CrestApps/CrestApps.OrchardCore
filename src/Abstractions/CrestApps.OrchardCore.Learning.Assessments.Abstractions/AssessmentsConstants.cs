namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Names shared by the Learning assessment projects.
/// </summary>
public static class AssessmentsConstants
{
    /// <summary>
    /// The course item kind of a quiz.
    /// </summary>
    public const string QuizKind = "Quiz";

    /// <summary>
    /// The kind of a quiz attempt in the grading list.
    /// </summary>
    public const string AttemptGradingQueueKind = "Attempt";

    /// <summary>
    /// The kind of Learning secret that holds the access code of a quiz.
    /// </summary>
    public const string QuizAccessCodeSecret = "QuizAccessCode";

    /// <summary>
    /// The name of the number sequence of attempts.
    /// </summary>
    public const string AttemptNumberSeries = "LearningAttempt";

    /// <summary>
    /// The name of the attempts recipe and deployment step.
    /// </summary>
    public const string AttemptsRecipeStep = "LearningAttempts";

    /// <summary>
    /// The name of the grade scales recipe and deployment step.
    /// </summary>
    public const string GradeScalesRecipeStep = "LearningGradeScales";

    /// <summary>
    /// The name of the assessments data source.
    /// </summary>
    public const string DataSourceName = "LearningAssessments";

    /// <summary>
    /// The Audit Trail category of assessment events.
    /// </summary>
    public const string AuditTrailCategory = "LearningAssessments";

    /// <summary>
    /// The feature ids.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Questions, banks, the quiz item and attempts.
        /// </summary>
        public const string Assessments = "CrestApps.OrchardCore.Learning.Assessments";

        /// <summary>
        /// Grade items, grade scales and the course gradebook.
        /// </summary>
        public const string Gradebook = "CrestApps.OrchardCore.Learning.Assessments.Gradebook";

        /// <summary>
        /// Imports QTI 3.0 and QTI 2.1 assessment items as questions, as a ContentTransfer file format.
        /// </summary>
        public const string QtiImport = "CrestApps.OrchardCore.Learning.Assessments.QtiImport";
    }

    /// <summary>
    /// The content types the migration creates.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A question bank, which lists its questions.
        /// </summary>
        public const string QuestionBank = "QuestionBank";

        /// <summary>
        /// The quiz course item.
        /// </summary>
        public const string Quiz = "QuizLesson";

        /// <summary>
        /// A question with one correct option.
        /// </summary>
        public const string SingleChoiceQuestion = "SingleChoiceQuestion";

        /// <summary>
        /// A question with one or more correct options.
        /// </summary>
        public const string MultipleChoiceQuestion = "MultipleChoiceQuestion";

        /// <summary>
        /// A true or false question.
        /// </summary>
        public const string TrueFalseQuestion = "TrueFalseQuestion";

        /// <summary>
        /// A question answered with a short text.
        /// </summary>
        public const string ShortTextQuestion = "ShortTextQuestion";

        /// <summary>
        /// A question answered with a number within a tolerance.
        /// </summary>
        public const string NumericQuestion = "NumericQuestion";

        /// <summary>
        /// A question that matches prompts to answers.
        /// </summary>
        public const string MatchingQuestion = "MatchingQuestion";

        /// <summary>
        /// A question that puts items in order.
        /// </summary>
        public const string OrderingQuestion = "OrderingQuestion";

        /// <summary>
        /// A text with blanks to fill in.
        /// </summary>
        public const string FillInBlanksQuestion = "FillInBlanksQuestion";

        /// <summary>
        /// An essay a grader marks.
        /// </summary>
        public const string EssayQuestion = "EssayQuestion";

        /// <summary>
        /// A question every answer is accepted in, such as an opinion; ungraded at zero points.
        /// </summary>
        public const string SurveyQuestion = "SurveyQuestion";

        /// <summary>
        /// A text whose blanks the learner fills by dragging words from a list that holds distractors.
        /// </summary>
        public const string DragIntoTextQuestion = "DragIntoTextQuestion";

        /// <summary>
        /// A text whose blanks are drop-down lists of words that hold distractors.
        /// </summary>
        public const string GapSelectQuestion = "GapSelectQuestion";

        /// <summary>
        /// A block of text between questions, with nothing to answer.
        /// </summary>
        public const string DescriptionQuestion = "DescriptionQuestion";
    }

    /// <summary>
    /// The question types, each scored by an <see cref="Services.IQuestionScorer"/> of the same name.
    /// </summary>
    public static class QuestionTypes
    {
        /// <summary>One correct option.</summary>
        public const string SingleChoice = "SingleChoice";

        /// <summary>One or more correct options.</summary>
        public const string MultipleChoice = "MultipleChoice";

        /// <summary>True or false.</summary>
        public const string TrueFalse = "TrueFalse";

        /// <summary>A short text.</summary>
        public const string ShortText = "ShortText";

        /// <summary>A number within a tolerance.</summary>
        public const string Numeric = "Numeric";

        /// <summary>Prompts matched to answers.</summary>
        public const string Matching = "Matching";

        /// <summary>Items put in order.</summary>
        public const string Ordering = "Ordering";

        /// <summary>Blanks in a text.</summary>
        public const string FillInBlanks = "FillInBlanks";

        /// <summary>An essay a grader marks.</summary>
        public const string Essay = "Essay";

        /// <summary>Every answer accepted; ungraded at zero points.</summary>
        public const string Survey = "Survey";

        /// <summary>Words dragged into the blanks of a text.</summary>
        public const string DragIntoText = "DragIntoText";

        /// <summary>Drop-down blanks in a text.</summary>
        public const string GapSelect = "GapSelect";

        /// <summary>A text between questions, with nothing to answer.</summary>
        public const string Description = "Description";
    }

    /// <summary>
    /// The notification types.
    /// </summary>
    public static class NotificationTypes
    {
        /// <summary>
        /// An attempt was graded.
        /// </summary>
        public const string Graded = "LearningAttemptGraded";
    }
}
