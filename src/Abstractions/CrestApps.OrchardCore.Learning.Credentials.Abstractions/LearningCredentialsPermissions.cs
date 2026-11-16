using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Credentials;

/// <summary>
/// The permissions of Digital Credentials. A learner sees their own credentials with Learning's Access learning.
/// </summary>
public static class LearningCredentialsPermissions
{
    /// <summary>
    /// Create, edit, publish and delete credential definitions.
    /// </summary>
    public static readonly Permission ManageCredentialDefinitions = new(
        "ManageCredentialDefinitions",
        LocalizationSource.Create("Manage credential definitions", typeof(LearningCredentialsPermissions)));

    /// <summary>
    /// Award a credential by hand and revoke an award.
    /// </summary>
    public static readonly Permission AwardCredentials = new(
        "AwardCredentials",
        LocalizationSource.Create("Award and revoke digital credentials", typeof(LearningCredentialsPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// See every credential award.
    /// </summary>
    public static readonly Permission ViewCredentialAwards = new(
        "ViewCredentialAwards",
        LocalizationSource.Create("View digital credential awards", typeof(LearningCredentialsPermissions)),
        [AwardCredentials]);
}
