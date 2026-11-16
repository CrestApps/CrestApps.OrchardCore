using System.IO.Compression;
using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.FileStorage;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// The protected file area of imported packages. No access policy is registered for it, so no signed link ever opens a
/// package file; package files are served only by the launch endpoint, for one launch.
/// </summary>
public sealed class LearningPackageFiles
{
}

/// <summary>
/// The limits of package uploads.
/// </summary>
public sealed class LearningPackageOptions
{
    /// <summary>
    /// Gets or sets the largest upload, in bytes.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most entries a package may have.
    /// </summary>
    public int MaxEntries { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the most bytes a package may take once extracted.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = 1024L * 1024 * 1024;
}

/// <summary>
/// Why an import was refused, besides the archive and manifest errors.
/// </summary>
public static class PackageImportErrors
{
    /// <summary>The file is not a .zip file.</summary>
    public const string NotZip = "NotZip";

    /// <summary>The upload is larger than allowed.</summary>
    public const string UploadTooLarge = "UploadTooLarge";

    /// <summary>The protected store refused the package, for instance an antivirus scanner.</summary>
    public const string Refused = "Refused";
}

/// <summary>
/// What an import did.
/// </summary>
public sealed class PackageImportResult
{
    /// <summary>Gets or sets the error: one of <see cref="PackageImportErrors"/>, <see cref="PackageArchiveErrors"/> or
    /// <see cref="ScormManifestErrors"/>; <see langword="null"/> when the package was imported.</summary>
    public string Error { get; set; }

    /// <summary>Gets or sets the archive entry that was refused, when there is one.</summary>
    public string Entry { get; set; }

    /// <summary>Gets or sets the package file kept in the protected store.</summary>
    public ProtectedFile File { get; set; }

    /// <summary>Gets or sets the manifest.</summary>
    public ScormManifest Manifest { get; set; }

    /// <summary>Gets or sets how many files the package has.</summary>
    public int FileCount { get; set; }

    /// <summary>Gets or sets the bytes of the files once extracted.</summary>
    public long ExpandedSize { get; set; }

    /// <summary>Gets whether the package was imported.</summary>
    public bool Succeeded => Error is null;

    /// <summary>
    /// Writes what was imported onto a package part.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="fileName">The uploaded file name.</param>
    /// <param name="importedBy">The user id of the importer.</param>
    /// <param name="importedUtc">When, in UTC.</param>
    public void ApplyTo(LearningPackagePart part, string fileName, string importedBy, DateTime importedUtc)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (!Succeeded)
        {
            throw new InvalidOperationException("A refused package cannot be applied.");
        }

        var launch = Manifest.Launch;

        part.PackageFileId = File.FileId;
        part.FileName = fileName;
        part.Standard = Manifest.Standard;
        part.ManifestIdentifier = Manifest.Identifier;
        part.Title = Manifest.Title;
        part.LaunchItemIdentifier = launch.ItemIdentifier;
        part.LaunchItemTitle = launch.Title;
        part.LaunchPath = launch.Path;
        part.LaunchQuery = launch.Query;
        part.MasteryScore = launch.MasteryScore;
        part.MaxTimeAllowed = launch.MaxTimeAllowed;
        part.TimeLimitAction = launch.TimeLimitAction;
        part.LaunchData = launch.DataFromLms;

        // The SCORM 2004 values; a SCORM 1.2 package clears what an earlier SCORM 2004 import wrote.
        part.CompletionThreshold = launch.CompletionThreshold;
        part.ScaledPassingScore = launch.ScaledPassingScore;
        part.ObjectiveIds = [.. launch.ObjectiveIds];
        part.HasSequencing = Manifest.HasSequencing;
        part.ScoCount = Manifest.Scos.Count;
        part.FileCount = FileCount;
        part.ExpandedSize = ExpandedSize;
        part.ImportedUtc = importedUtc;
        part.ImportedBy = importedBy;
    }

    internal static PackageImportResult Fail(string error, string entry = null)
        => new() { Error = error, Entry = entry };
}

/// <summary>
/// Imports an uploaded SCORM 1.2 or SCORM 2004 package: the archive is extracted into a temporary folder with safe paths and within
/// the limits, its manifest is read, the launch file is checked, and a clean archive of only the extracted files is kept
/// in the protected store, through Orchard Core's file creation pipeline (an antivirus scanner, for one). The temporary
/// folder is always deleted.
/// </summary>
public sealed class LearningPackageImporter
{
    private readonly ITempDirectoryProvider _tempDirectoryProvider;
    private readonly IProtectedFileStore<LearningPackageFiles> _files;
    private readonly LearningPackageOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningPackageImporter"/> class.
    /// </summary>
    public LearningPackageImporter(
        ITempDirectoryProvider tempDirectoryProvider,
        IProtectedFileStore<LearningPackageFiles> files,
        IOptions<LearningPackageOptions> options,
        ILogger<LearningPackageImporter> logger)
    {
        _tempDirectoryProvider = tempDirectoryProvider;
        _files = files;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Imports a package.
    /// </summary>
    /// <param name="fileName">The uploaded file name.</param>
    /// <param name="upload">The uploaded content.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was imported, or why the package was refused.</returns>
    public async Task<PackageImportResult> ImportAsync(string fileName, Stream upload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);

        if (!string.Equals(Path.GetExtension(fileName ?? string.Empty), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return PackageImportResult.Fail(PackageImportErrors.NotZip);
        }

        if (upload.CanSeek && upload.Length - upload.Position > _options.MaxUploadBytes)
        {
            return PackageImportResult.Fail(PackageImportErrors.UploadTooLarge);
        }

        var folder = _tempDirectoryProvider.CreateTempSubdirectory("learning-package-");

        try
        {
            // The upload is copied first, so the archive is read from a seekable file of a known, bounded size.
            var uploadPath = Path.Combine(folder, "upload.zip");

            if (!await CopyBoundedAsync(upload, uploadPath, _options.MaxUploadBytes, cancellationToken))
            {
                return PackageImportResult.Fail(PackageImportErrors.UploadTooLarge);
            }

            var extracted = Path.Combine(folder, "files");
            Directory.CreateDirectory(extracted);

            PackageArchiveResult archive;

            await using (var source = new FileStream(uploadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                archive = await PackageArchiveExtractor.ExtractAsync(source, extracted, new PackageArchiveLimits
                {
                    MaxEntries = _options.MaxEntries,
                    MaxExpandedBytes = _options.MaxExpandedBytes,
                }, cancellationToken);
            }

            if (!archive.Succeeded)
            {
                return PackageImportResult.Fail(archive.Error, archive.Entry);
            }

            var manifestPath = archive.Files.FirstOrDefault(file => string.Equals(file, "imsmanifest.xml", StringComparison.OrdinalIgnoreCase));

            if (manifestPath is null)
            {
                return PackageImportResult.Fail(archive.Files.Any(file => string.Equals(file, "cmi5.xml", StringComparison.OrdinalIgnoreCase))
                    ? ScormManifestErrors.Cmi5
                    : ScormManifestErrors.Missing);
            }

            ScormManifestResult parsed;

            await using (var manifest = new FileStream(Path.Combine(extracted, manifestPath), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                parsed = ScormManifestParser.Parse(manifest);
            }

            if (!parsed.Succeeded)
            {
                return PackageImportResult.Fail(parsed.Error);
            }

            // Every SCO must open a file of the package. A path in another case is matched to the file as it is
            // stored, because the stored archive is read by its exact entry names.
            foreach (var sco in parsed.Manifest.Scos)
            {
                var file = archive.Files.FirstOrDefault(candidate => string.Equals(candidate, sco.Path, StringComparison.Ordinal))
                    ?? archive.Files.FirstOrDefault(candidate => string.Equals(candidate, sco.Path, StringComparison.OrdinalIgnoreCase));

                if (file is null)
                {
                    return PackageImportResult.Fail(ScormManifestErrors.LaunchFileMissing);
                }

                sco.Path = file;
            }

            // Only the files that passed the checks are kept, under their normalized names.
            var cleanPath = Path.Combine(folder, "package.zip");

            using (var clean = ZipFile.Open(cleanPath, ZipArchiveMode.Create))
            {
                foreach (var file in archive.Files)
                {
                    clean.CreateEntryFromFile(Path.Combine(extracted, file.Replace('/', Path.DirectorySeparatorChar)), file, CompressionLevel.Optimal);
                }
            }

            ProtectedFile stored;

            try
            {
                await using var content = new FileStream(cleanPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                stored = await _files.SaveAsync(Path.GetFileName(fileName), content, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "A learning package was refused by the protected store.");

                return PackageImportResult.Fail(PackageImportErrors.Refused);
            }

            return new PackageImportResult
            {
                File = stored,
                Manifest = parsed.Manifest,
                FileCount = archive.Files.Count,
                ExpandedSize = archive.ExpandedSize,
            };
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private static async Task<bool> CopyBoundedAsync(Stream source, string path, long maxBytes, CancellationToken cancellationToken)
    {
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;

            if (total > maxBytes)
            {
                return false;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "The temporary folder of a learning package import could not be deleted.");
        }
    }
}
