using CrestApps.OrchardCore.Learning.Core.Archives;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// Says why <see cref="LearningPackageImporter"/> refused a package, in words for the person who imported it: the
/// package editor's upload and every other importer of packages (the Moodle import) show the same reasons.
/// </summary>
public sealed class PackageImportErrorText
{
    private readonly LearningPackageOptions _options;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageImportErrorText"/> class.
    /// </summary>
    /// <param name="options">The package limits.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public PackageImportErrorText(IOptions<LearningPackageOptions> options, IStringLocalizer<PackageImportErrorText> stringLocalizer)
    {
        _options = options.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the reason a package was refused.
    /// </summary>
    /// <param name="result">The refused import.</param>
    /// <returns>The reason.</returns>
    public LocalizedString Get(PackageImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Error switch
        {
            PackageImportErrors.NotZip => S["The file is not a .zip file."],
            PackageImportErrors.UploadTooLarge => S["The file is larger than {0} MB.", _options.MaxUploadBytes / (1024 * 1024)],
            PackageImportErrors.Refused => S["The package was refused, for instance by the virus scanner."],
            PackageImportErrors.FileType => S["The package has a file of a kind a learning package does not hold: {0}.", result.Entry],
            PackageArchiveErrors.InvalidArchive => S["The file is not a valid .zip archive."],
            PackageArchiveErrors.UnsafePath => S["The package has a file with a path that is not allowed: {0}.", result.Entry],
            PackageArchiveErrors.SymbolicLink => S["The package has a symbolic link, which is not allowed: {0}.", result.Entry],
            PackageArchiveErrors.TooManyEntries => S["The package has more than {0} files.", _options.MaxEntries],
            PackageArchiveErrors.TooLarge => S["The package is larger than {0} MB once extracted.", _options.MaxExpandedBytes / (1024 * 1024)],
            PackageArchiveErrors.DuplicateEntry => S["The package has two files with the same path: {0}.", result.Entry],
            PackageArchiveErrors.Empty => S["The package has no files."],
            ScormManifestErrors.Missing => S["The package has no imsmanifest.xml at its root."],
            ScormManifestErrors.InvalidXml => S["The imsmanifest.xml of the package is not valid XML."],
            ScormManifestErrors.Cmi5 => S["The package is a cmi5 course. Turn on the Learning cmi5 Packages feature to import it."],
            Cmi5Errors.InvalidXml => S["The cmi5.xml of the package is not valid XML."],
            Cmi5Errors.NoCourse => S["The cmi5.xml has no course with an id and a title."],
            Cmi5Errors.NoAu => S["The cmi5 course has no AU."],
            Cmi5Errors.InvalidId => S["An AU or a block of the cmi5 course has no id that is an IRI, or two have the same id."],
            Cmi5Errors.InvalidUrl => S["An AU of the cmi5 course has no URL, or a path that is not allowed."],
            Cmi5Errors.NotHttps => S["An AU of the cmi5 course opens a web address that is not https."],
            Cmi5Errors.InvalidMoveOn => S["An AU of the cmi5 course has a moveOn that cmi5 does not define."],
            Cmi5Errors.InvalidMasteryScore => S["The mastery score of an AU is not a number from 0 to 1."],
            Cmi5Errors.InvalidLaunchMethod => S["An AU of the cmi5 course has a launch method that cmi5 does not define."],
            Cmi5Errors.LaunchFileMissing => S["An AU of the cmi5 course opens a file that is not in the package."],
            ScormManifestErrors.NoOrganization => S["The manifest has no organization with items."],
            ScormManifestErrors.NoSco => S["The manifest has no SCO to launch; a package of assets only cannot report progress."],
            ScormManifestErrors.ExternalLaunch => S["A SCO of the package opens a web address outside the package, which is not supported."],
            ScormManifestErrors.UnsafeHref => S["A SCO of the package opens a file path that is not allowed."],
            ScormManifestErrors.LaunchFileMissing => S["A SCO of the package opens a file that is not in the package."],
            ScormManifestErrors.InvalidMasteryScore => S["The mastery score of a SCO is not a number from 0 to 100."],
            ScormManifestErrors.InvalidCompletionThreshold => S["The completion threshold of a SCO is not a number from 0 to 1."],
            ScormManifestErrors.InvalidPassingScore => S["The minimum normalized measure of a SCO's primary objective is not a number from -1 to 1."],
            _ => S["The package could not be imported."],
        };
    }
}
