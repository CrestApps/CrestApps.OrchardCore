using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Xapi;

/// <summary>
/// The permissions of the Learning Record Store. An LRS client is an Orchard Core OpenID application whose roles hold
/// these permissions; the LRS endpoint never accepts the sign-in cookie.
/// </summary>
public static class LearningXapiPermissions
{
    /// <summary>
    /// Store statements in the LRS.
    /// </summary>
    public static readonly Permission WriteLearningStatements = new(
        "WriteLearningStatements",
        LocalizationSource.Create("Write learning statements to the Learning Record Store", typeof(LearningXapiPermissions)));

    /// <summary>
    /// Read every statement of the LRS, and see them in the admin.
    /// </summary>
    public static readonly Permission ReadAllLearningStatements = new(
        "ReadAllLearningStatements",
        LocalizationSource.Create("Read all learning statements of the Learning Record Store", typeof(LearningXapiPermissions)));

    /// <summary>
    /// Read the statements the client stored itself.
    /// </summary>
    public static readonly Permission ReadOwnLearningStatements = new(
        "ReadOwnLearningStatements",
        LocalizationSource.Create("Read the learning statements the client stored", typeof(LearningXapiPermissions)),
        [ReadAllLearningStatements]);

    /// <summary>
    /// Read, write and delete the state, activity profile and agent profile documents, and read the activities and
    /// agents resources.
    /// </summary>
    public static readonly Permission ManageLearningDocuments = new(
        "ManageLearningDocuments",
        LocalizationSource.Create("Manage the documents of the Learning Record Store", typeof(LearningXapiPermissions)));
}
