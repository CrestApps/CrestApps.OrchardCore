using OrchardCore;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Settings for signing in with an emailed code, bound from the <c>CrestApps:Users:EmailSignIn</c> configuration
/// section.
/// </summary>
public sealed class EmailSignInOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:Users:EmailSignIn";

    /// <summary>
    /// Gets or sets the roles whose members can't sign in with a code and keep using their password. A code is only as
    /// strong as the mailbox it goes to, so the most powerful accounts are left out by default.
    /// </summary>
    public string[] ExcludedRoles { get; set; } = [OrchardCoreConstants.Roles.Administrator];
}
