namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// The result of translating a survey's draft with AI.
/// </summary>
public sealed class SurveyTranslationResult
{
    /// <summary>
    /// Gets or sets why nothing was saved: <see cref="SurveyErrorCode.None"/> when the translations were saved (or
    /// nothing needed one), <see cref="SurveyErrorCode.NotFound"/> when the survey is missing or the user may not manage
    /// it, <see cref="SurveyErrorCode.NotAvailableNow"/> when no AI model answered, and <see cref="SurveyErrorCode.Busy"/>
    /// when someone changed the survey while the model translated it.
    /// </summary>
    public SurveyErrorCode Error { get; set; }

    /// <summary>
    /// Gets or sets how many texts were written into the draft.
    /// </summary>
    public int Written { get; set; }

    /// <summary>
    /// Gets whether the translations were saved.
    /// </summary>
    public bool Succeeded => Error == SurveyErrorCode.None;
}
