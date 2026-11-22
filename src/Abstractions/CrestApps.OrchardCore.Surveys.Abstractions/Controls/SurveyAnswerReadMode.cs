namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Says whether reading an answer may keep files.
/// </summary>
public enum SurveyAnswerReadMode
{
    /// <summary>
    /// The answers are read without keeping anything, as for a live evaluation, an autosave, an immediate-feedback
    /// check or going back: new files and drawn signatures are ignored, and the files saved before stay.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The page is posted forward: new files and drawn signatures are handed back as
    /// <see cref="SurveyAnswerReadContext.PendingFiles"/>, which the runtime stores only when the page passes
    /// validation.
    /// </summary>
    Commit = 1,
}
