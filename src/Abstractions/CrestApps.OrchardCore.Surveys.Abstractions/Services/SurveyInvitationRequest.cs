using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Who to invite to which survey.
/// </summary>
public sealed class SurveyInvitationRequest
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the invitation collector, or <see langword="null"/> for the survey's first one.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user to invite, or <see langword="null"/>.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the party to invite, or <see langword="null"/>.
    /// </summary>
    public PartyRef? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address to invite, or <see langword="null"/>.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name of the person invited, or <see langword="null"/>.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the response will belong to, or <see langword="null"/>.
    /// </summary>
    public RecordRef? Context { get; set; }

    /// <summary>
    /// Gets or sets when the link expires, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }
}
