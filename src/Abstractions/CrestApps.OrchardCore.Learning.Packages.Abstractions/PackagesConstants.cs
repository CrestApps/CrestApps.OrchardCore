namespace CrestApps.OrchardCore.Learning.Packages;

/// <summary>
/// The names Learning packages uses.
/// </summary>
public static class PackagesConstants
{
    /// <summary>
    /// The course item kind of a learning package.
    /// </summary>
    public const string PackageKind = "LearningPackage";

    /// <summary>
    /// The YesSql collection of runtime states.
    /// </summary>
    public const string CollectionName = "LearningPackageRuntime";

    /// <summary>
    /// The name of the runtime states recipe and deployment step.
    /// </summary>
    public const string RuntimeStatesRecipeStep = "LearningPackageRuntimeStates";

    /// <summary>
    /// The folder of the protected file area of imported packages, in the tenant's App_Data.
    /// </summary>
    public const string FilesFolder = "LearningPackages";

    /// <summary>
    /// The features of Learning packages.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// SCORM 1.2 packages.
        /// </summary>
        public const string Scorm = "CrestApps.OrchardCore.Learning.Packages.Scorm";
    }

    /// <summary>
    /// The content types Learning packages adds.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// A learning package in a course outline.
        /// </summary>
        public const string Package = "LearningPackage";
    }

    /// <summary>
    /// The package standards.
    /// </summary>
    public static class Standards
    {
        /// <summary>
        /// ADL SCORM 1.2.
        /// </summary>
        public const string Scorm12 = "SCORM 1.2";
    }
}
