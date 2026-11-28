namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// The context responses through an invitation's link belong to, kept in the invitation's properties.
/// </summary>
public sealed class SurveyInvitationContext
{
    /// <summary>
    /// Gets or sets the context as Kind:Id.
    /// </summary>
    public string Context { get; set; }
}
