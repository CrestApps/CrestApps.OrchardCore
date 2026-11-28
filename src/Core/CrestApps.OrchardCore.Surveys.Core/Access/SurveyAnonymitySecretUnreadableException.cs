namespace CrestApps.OrchardCore.Surveys.Core.Access;

/// <summary>
/// Thrown when a survey's protected anonymity secret can't be read, because the ASP.NET Data Protection keys that
/// protected it are not available (a key ring that isn't persisted, web-farm nodes that don't share keys, or a database
/// restored on another host). The secret is never replaced on its own: respondent limits and invitation links depend on
/// it, so the survey fails closed until the keys come back or an administrator resets the secret on the survey's access
/// and settings page.
/// </summary>
public sealed class SurveyAnonymitySecretUnreadableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymitySecretUnreadableException"/> class.
    /// </summary>
    public SurveyAnonymitySecretUnreadableException()
        : base("The anonymity secret of the survey could not be read.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymitySecretUnreadableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SurveyAnonymitySecretUnreadableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymitySecretUnreadableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The Data Protection error.</param>
    public SurveyAnonymitySecretUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets the id of the survey, when known.
    /// </summary>
    public string SurveyId { get; init; }
}
