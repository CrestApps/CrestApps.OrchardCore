namespace CrestApps.OrchardCore.Learning.Surveys;

/// <summary>
/// The constants of Learning Surveys: the course survey and choice course items, whose questions live in a survey of
/// the Surveys module that each item owns.
/// </summary>
public static class LearningSurveysConstants
{
    /// <summary>
    /// The feature id.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Learning.Surveys";

    /// <summary>
    /// The course item kind of course surveys and choices.
    /// </summary>
    public const string SurveyKind = "Survey";

    /// <summary>
    /// The name of the survey template feedback survey items start from, which this feature seeds.
    /// </summary>
    public const string CourseFeedbackTemplate = "course-feedback";

    /// <summary>
    /// The content types.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A survey of several questions about the course.
        /// </summary>
        public const string FeedbackSurvey = "FeedbackSurvey";

        /// <summary>
        /// One question learners answer by picking a choice, as a poll.
        /// </summary>
        public const string Choice = "Choice";
    }
}
