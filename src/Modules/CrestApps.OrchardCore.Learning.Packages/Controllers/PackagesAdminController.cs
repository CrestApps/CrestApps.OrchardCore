using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Controllers;

/// <summary>
/// Imports a package into a learning package item. The person needs Import learning packages and may edit the item.
/// The import goes into a draft of the item, so learners get the new package once it is published.
/// </summary>
[Admin]
public sealed class PackagesAdminController : Controller
{
    /// <summary>
    /// The largest upload request: the largest package and the form around it.
    /// </summary>
    internal const long MaxRequestBytes = 520L * 1024 * 1024;

    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly LearningPackageImporter _importer;
    private readonly LearningPackageOptions _options;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagesAdminController"/> class.
    /// </summary>
    public PackagesAdminController(
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        LearningPackageImporter importer,
        IOptions<LearningPackageOptions> options,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PackagesAdminController> htmlLocalizer,
        IStringLocalizer<PackagesAdminController> stringLocalizer)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _importer = importer;
        _options = options.Value;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the upload form of a package item.
    /// </summary>
    /// <param name="contentItemId">The package item.</param>
    /// <returns>The form.</returns>
    [Admin("learning/packages/{contentItemId}/upload", "LearningPackageUpload")]
    public async Task<IActionResult> Upload(string contentItemId)
    {
        var (item, refusal) = await AuthorizeAsync(contentItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        return View(BuildModel(item));
    }

    /// <summary>
    /// Imports an uploaded package into a draft of the item.
    /// </summary>
    /// <param name="contentItemId">The package item.</param>
    /// <param name="package">The .zip file.</param>
    /// <returns>The item's editor, or the form with the reason.</returns>
    [HttpPost]
    [Admin("learning/packages/{contentItemId}/import", "LearningPackageImport")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Import(string contentItemId, IFormFile package)
    {
        var (item, refusal) = await AuthorizeAsync(contentItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        if (package is null || package.Length == 0)
        {
            ModelState.AddModelError(nameof(package), S["Choose a .zip file of a SCORM 1.2 package."]);

            return View(nameof(Upload), BuildModel(item));
        }

        PackageImportResult result;

        await using (var content = package.OpenReadStream())
        {
            result = await _importer.ImportAsync(package.FileName, content, HttpContext.RequestAborted);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(package), ErrorText(result));

            return View(nameof(Upload), BuildModel(item));
        }

        var draft = await _contentManager.GetAsync(item.ContentItemId, VersionOptions.DraftRequired);

        draft.Alter<LearningPackagePart>(part => result.ApplyTo(part, Path.GetFileName(package.FileName), User.FindFirstValue(ClaimTypes.NameIdentifier), _clock.UtcNow));
        await _contentManager.UpdateAsync(draft);
        await _contentManager.SaveDraftAsync(draft);

        await _notifier.SuccessAsync(result.Manifest.Scos.Count > 1
            ? H["The package \"{0}\" was imported. It has {1} SCOs; learners launch the first one, \"{2}\". Publish the item to give it to learners.", result.Manifest.Title, result.Manifest.Scos.Count, result.Manifest.Launch.Title]
            : H["The package \"{0}\" was imported. Publish the item to give it to learners.", result.Manifest.Title]);

        return RedirectToAction("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = item.ContentItemId });
    }

    private async Task<(ContentItem Item, IActionResult Refusal)> AuthorizeAsync(string contentItemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningPackagesPermissions.ImportLearningPackages))
        {
            return (null, Forbid());
        }

        var item = string.IsNullOrEmpty(contentItemId) ? null : await _contentManager.GetAsync(contentItemId, VersionOptions.Latest);

        if (item is null || !item.TryGet<LearningPackagePart>(out _))
        {
            return (null, NotFound());
        }

        if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, item))
        {
            return (null, Forbid());
        }

        return (item, null);
    }

    private PackageUploadViewModel BuildModel(ContentItem item)
        => new()
        {
            ContentItemId = item.ContentItemId,
            Title = item.DisplayText,
            Part = item.TryGet<LearningPackagePart>(out var part) ? part : null,
            MaxUploadMb = _options.MaxUploadBytes / (1024 * 1024),
        };

    private string ErrorText(PackageImportResult result)
        => result.Error switch
        {
            PackageImportErrors.NotZip => S["The file is not a .zip file."],
            PackageImportErrors.UploadTooLarge => S["The file is larger than {0} MB.", _options.MaxUploadBytes / (1024 * 1024)],
            PackageImportErrors.Refused => S["The package was refused, for instance by the virus scanner."],
            PackageArchiveErrors.InvalidArchive => S["The file is not a valid .zip archive."],
            PackageArchiveErrors.UnsafePath => S["The package has a file with a path that is not allowed: {0}.", result.Entry],
            PackageArchiveErrors.SymbolicLink => S["The package has a symbolic link, which is not allowed: {0}.", result.Entry],
            PackageArchiveErrors.TooManyEntries => S["The package has more than {0} files.", _options.MaxEntries],
            PackageArchiveErrors.TooLarge => S["The package is larger than {0} MB once extracted.", _options.MaxExpandedBytes / (1024 * 1024)],
            PackageArchiveErrors.DuplicateEntry => S["The package has two files with the same path: {0}.", result.Entry],
            PackageArchiveErrors.Empty => S["The package has no files."],
            Scorm12ManifestErrors.Missing => S["The package has no imsmanifest.xml at its root."],
            Scorm12ManifestErrors.InvalidXml => S["The imsmanifest.xml of the package is not valid XML."],
            Scorm12ManifestErrors.Scorm2004 => S["The package is SCORM 2004, which is not supported yet. Import a SCORM 1.2 package."],
            Scorm12ManifestErrors.Cmi5 => S["The package is a cmi5 course, which is not supported yet. Import a SCORM 1.2 package."],
            Scorm12ManifestErrors.NoOrganization => S["The manifest has no organization with items."],
            Scorm12ManifestErrors.NoSco => S["The manifest has no SCO to launch; a package of assets only cannot report progress."],
            Scorm12ManifestErrors.ExternalLaunch => S["A SCO of the package opens a web address outside the package, which is not supported."],
            Scorm12ManifestErrors.UnsafeHref => S["A SCO of the package opens a file path that is not allowed."],
            Scorm12ManifestErrors.LaunchFileMissing => S["A SCO of the package opens a file that is not in the package."],
            Scorm12ManifestErrors.InvalidMasteryScore => S["The mastery score of a SCO is not a number from 0 to 100."],
            _ => S["The package could not be imported."],
        };
}
