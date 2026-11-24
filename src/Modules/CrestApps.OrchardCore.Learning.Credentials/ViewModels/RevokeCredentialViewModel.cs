using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Credentials.ViewModels;

/// <summary>
/// The form that revokes an award.
/// </summary>
public class RevokeCredentialViewModel
{
    /// <summary>
    /// Gets or sets why it is revoked.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the award id.
    /// </summary>
    [BindNever]
    public string AwardId { get; set; }

    /// <summary>
    /// Gets or sets the achievement's name.
    /// </summary>
    [BindNever]
    public string AchievementName { get; set; }

    /// <summary>
    /// Gets or sets the recipient's name.
    /// </summary>
    [BindNever]
    public string RecipientName { get; set; }

    /// <summary>
    /// Gets or sets whether it is already revoked.
    /// </summary>
    [BindNever]
    public bool AlreadyRevoked { get; set; }
}
