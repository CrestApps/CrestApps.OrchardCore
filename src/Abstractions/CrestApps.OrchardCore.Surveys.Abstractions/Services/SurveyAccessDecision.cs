namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The decision of an access rule.
/// </summary>
public sealed class SurveyAccessDecision
{
    /// <summary>
    /// A decision that allows.
    /// </summary>
    public static readonly SurveyAccessDecision Allow = new() { Error = SurveyErrorCode.None };

    /// <summary>
    /// Gets or sets the error, or <see cref="SurveyErrorCode.None"/> to allow.
    /// </summary>
    public SurveyErrorCode Error { get; init; }

    /// <summary>
    /// Gets or sets a message of the rule's own, or <see langword="null"/> for the message of the error code.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    /// Gets whether the decision allows.
    /// </summary>
    public bool IsAllowed => Error == SurveyErrorCode.None;

    /// <summary>
    /// Makes a refusal.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="message">A message of the rule's own, or <see langword="null"/>.</param>
    /// <returns>The decision.</returns>
    public static SurveyAccessDecision Deny(SurveyErrorCode error, string message = null)
        => new() { Error = error, Message = message };
}
