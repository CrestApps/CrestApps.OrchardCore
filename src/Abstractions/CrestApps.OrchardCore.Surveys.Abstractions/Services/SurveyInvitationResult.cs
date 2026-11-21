using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of creating an invitation.
/// </summary>
public sealed class SurveyInvitationResult
{
    /// <summary>
    /// Gets or sets the error, or <see cref="SurveyErrorCode.None"/>.
    /// </summary>
    public SurveyErrorCode Error { get; set; }

    /// <summary>
    /// Gets or sets the invitation, or <see langword="null"/>.
    /// </summary>
    public SurveyInvitation Invitation { get; set; }

    /// <summary>
    /// Gets or sets the personal link, or <see langword="null"/>. It is shown once: only the token's hash is stored.
    /// </summary>
    public string Link { get; set; }

    /// <summary>
    /// Gets whether the invitation was created.
    /// </summary>
    public bool Succeeded => Error == SurveyErrorCode.None && Invitation is not null;
}
