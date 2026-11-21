namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// What a question type can do. The designer, the runner and the reports read these flags instead of the type name.
/// </summary>
[Flags]
public enum SurveyControlCapabilities
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// It collects an answer (a text block does not).
    /// </summary>
    Answerable = 1,

    /// <summary>
    /// The editor shows the choice list; the question supports shuffled choices, carried-forward choices and choice
    /// visibility.
    /// </summary>
    UsesChoices = 2,

    /// <summary>
    /// The editor shows rows (matrix rows, matching prompts, items).
    /// </summary>
    UsesRows = 4,

    /// <summary>
    /// It has a scorer.
    /// </summary>
    Scorable = 8,

    /// <summary>
    /// Its scorer may leave the score to a grader.
    /// </summary>
    ManuallyGraded = 16,

    /// <summary>
    /// Other questions may test its answer.
    /// </summary>
    ConditionSource = 32,

    /// <summary>
    /// It supports an other choice with a text box.
    /// </summary>
    SupportsOther = 64,

    /// <summary>
    /// It supports validation rules.
    /// </summary>
    SupportsValidation = 128,

    /// <summary>
    /// It has an analyzer that summarizes its answers.
    /// </summary>
    Aggregatable = 256,

    /// <summary>
    /// It stores uploaded files.
    /// </summary>
    StoresFiles = 512,

    /// <summary>
    /// It ships a script that raises the survey:change event.
    /// </summary>
    ClientScript = 1024,
}
