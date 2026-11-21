namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// The editor of a Surveys workflow task. Each task shows the settings it uses.
/// </summary>
public class SurveyTaskViewModel
{
    /// <summary>
    /// Gets or sets the survey id.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the response id.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name of the person.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the context kind.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets the context id.
    /// </summary>
    public string ContextId { get; set; }

    /// <summary>
    /// Gets or sets in how many days a link expires.
    /// </summary>
    public string ExpiresInDays { get; set; }

    /// <summary>
    /// Gets or sets the extra minutes.
    /// </summary>
    public string ExtraMinutes { get; set; }

    /// <summary>
    /// Gets or sets the extra attempts.
    /// </summary>
    public string ExtraAttempts { get; set; }

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// Gets or sets the property value.
    /// </summary>
    public string PropertyValue { get; set; }

    /// <summary>
    /// Gets or sets the output name.
    /// </summary>
    public string OutputName { get; set; }
}
