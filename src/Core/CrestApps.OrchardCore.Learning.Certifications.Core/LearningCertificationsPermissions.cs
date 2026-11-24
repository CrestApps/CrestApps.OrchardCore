using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Certifications.Core;

/// <summary>
/// The permissions of education credits.
/// </summary>
public static class LearningCertificationsPermissions
{
    /// <summary>
    /// Create and change education credit types, and award credits by hand.
    /// </summary>
    public static readonly Permission ManageEducationCredits = new(
        "ManageEducationCredits",
        LocalizationSource.Create("Manage education credit types and award credits", typeof(LearningCertificationsPermissions)));

    /// <summary>
    /// Accept or reject the external learning people record.
    /// </summary>
    public static readonly Permission ReviewExternalLearning = new(
        "ReviewExternalLearning",
        LocalizationSource.Create("Accept or reject external learning", typeof(LearningCertificationsPermissions)),
        [ManageEducationCredits]);
}
