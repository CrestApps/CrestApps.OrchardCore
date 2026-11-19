using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.Packages;

/// <summary>
/// The permissions of Learning packages.
/// </summary>
public static class LearningPackagesPermissions
{
    /// <summary>
    /// Upload a package file to a learning package item the person may edit. Package content is script that runs for
    /// learners, so importing it is its own permission.
    /// </summary>
    public static readonly Permission ImportLearningPackages = new(
        "ImportLearningPackages",
        LocalizationSource.Create("Import learning packages", typeof(LearningPackagesPermissions)),
        [LearningPermissions.ManageCourses]);

    /// <summary>
    /// Change the learning package settings, such as the package content host the package files are served from.
    /// </summary>
    public static readonly Permission ManageLearningPackageSettings = new(
        "ManageLearningPackageSettings",
        LocalizationSource.Create("Manage learning package settings", typeof(LearningPackagesPermissions)));
}
