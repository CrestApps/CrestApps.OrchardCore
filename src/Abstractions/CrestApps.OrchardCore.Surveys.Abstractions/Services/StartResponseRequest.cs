using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What to start a response with.
/// </summary>
public sealed class StartResponseRequest
{
    /// <summary>
    /// Gets or sets the id of the survey; the collector token may be given instead.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the token of the collector; the default collector is used when it is empty.
    /// </summary>
    public string CollectorToken { get; set; }

    /// <summary>
    /// Gets or sets the token of a personal invitation, or <see langword="null"/>.
    /// </summary>
    public string InvitationToken { get; set; }

    /// <summary>
    /// Gets or sets who responds.
    /// </summary>
    public SurveyRespondentIdentity Identity { get; set; } = new();

    /// <summary>
    /// Gets or sets what the response belongs to, set by consumers (such as an enrollment).
    /// </summary>
    public RecordRef? Context { get; set; }

    /// <summary>
    /// Gets or sets hidden variable values from the consumer (highest precedence).
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// Gets or sets values read from the link's query string; only variables that accept them are kept.
    /// </summary>
    public Dictionary<string, string> QueryVariables { get; set; } = [];

    /// <summary>
    /// Gets or sets data of the consumer stored on the response.
    /// </summary>
    public JsonObject Properties { get; set; }

    /// <summary>
    /// Gets or sets whether this is a preview or test response.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// Gets or sets whether a test response runs the draft instead of the published version.
    /// </summary>
    public bool UseDraft { get; set; }

    /// <summary>
    /// Gets or sets the collector password entered, or <see langword="null"/>.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets whether the respondent accepted the survey's statement (<see cref="Models.SurveyAccessSettings.Statement"/>).
    /// </summary>
    public bool StatementAccepted { get; set; }

    /// <summary>
    /// Gets or sets the culture, or <see langword="null"/>.
    /// </summary>
    public string Culture { get; set; }
}
