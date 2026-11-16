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
        /// SCORM packages: SCORM 1.2 and SCORM 2004 (3rd and 4th Edition).
        /// </summary>
        public const string Scorm = "CrestApps.OrchardCore.Learning.Packages.Scorm";

        /// <summary>
        /// cmi5 packages, launched against the Learning Record Store.
        /// </summary>
        public const string Cmi5 = "CrestApps.OrchardCore.Learning.Packages.Cmi5";
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

        /// <summary>
        /// ADL SCORM 2004 3rd Edition.
        /// </summary>
        public const string Scorm2004ThirdEdition = "SCORM 2004 3rd Edition";

        /// <summary>
        /// ADL SCORM 2004 4th Edition.
        /// </summary>
        public const string Scorm2004FourthEdition = "SCORM 2004 4th Edition";

        /// <summary>
        /// ADL cmi5 (Quartz).
        /// </summary>
        public const string Cmi5 = "cmi5";

        /// <summary>
        /// Gets whether a standard is an edition of SCORM 2004.
        /// </summary>
        /// <param name="standard">The standard.</param>
        /// <returns><see langword="true"/> for SCORM 2004 3rd or 4th Edition.</returns>
        public static bool IsScorm2004(string standard)
            => standard is Scorm2004ThirdEdition or Scorm2004FourthEdition;
    }
}
