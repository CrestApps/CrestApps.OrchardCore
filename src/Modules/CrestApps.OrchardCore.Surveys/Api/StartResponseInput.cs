namespace CrestApps.OrchardCore.Surveys.Api;

/// <summary>
/// What to start a response with through the API.
/// </summary>
public sealed class StartResponseInput
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the token of a collector, or <see langword="null"/> for the default link.
    /// </summary>
    public string CollectorToken { get; set; }

    /// <summary>
    /// Gets or sets what the response belongs to, as Kind:Id, or <see langword="null"/>.
    /// </summary>
    public string Context { get; set; }

    /// <summary>
    /// Gets or sets the values of hidden variables.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets the collector's password (an access code), or <see langword="null"/>.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent accepted the survey's statement.
    /// </summary>
    public bool StatementAccepted { get; set; }

    /// <summary>
    /// Gets or sets the culture, or <see langword="null"/>.
    /// </summary>
    public string Culture { get; set; }
}
