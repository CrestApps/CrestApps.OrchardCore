using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;
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
    private readonly PackageImportErrorText _errorText;
    private readonly PackageContentHostProvider _contentHost;
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
        PackageImportErrorText errorText,
        PackageContentHostProvider contentHost,
        IOptions<LearningPackageOptions> options,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<PackagesAdminController> htmlLocalizer,
        IStringLocalizer<PackagesAdminController> stringLocalizer)
    {
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _importer = importer;
        _errorText = errorText;
        _contentHost = contentHost;
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
    [HttpGet]
    [Admin("learning/packages/{contentItemId}/upload", "LearningPackageUpload")]
    public async Task<IActionResult> Upload(string contentItemId)
    {
        var (item, refusal) = await AuthorizeAsync(contentItemId);

        if (refusal is not null)
        {
            return refusal;
        }

        return View(await BuildModelAsync(item));
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
            ModelState.AddModelError(nameof(package), _options.AcceptCmi5 ? S["Choose a .zip file of a SCORM 1.2, SCORM 2004 or cmi5 package."] : S["Choose a .zip file of a SCORM 1.2 or SCORM 2004 package."]);

            return View(nameof(Upload), await BuildModelAsync(item));
        }

        PackageImportResult result;

        await using (var content = package.OpenReadStream())
        {
            result = await _importer.ImportAsync(package.FileName, content, HttpContext.RequestAborted);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(package), _errorText.Get(result));

            return View(nameof(Upload), await BuildModelAsync(item));
        }

        var draft = await _contentManager.GetAsync(item.ContentItemId, VersionOptions.DraftRequired);

        draft.Alter<LearningPackagePart>(part => result.ApplyTo(part, Path.GetFileName(package.FileName), User.FindFirstValue(ClaimTypes.NameIdentifier), _clock.UtcNow));
        await _contentManager.UpdateAsync(draft);
        await _contentManager.SaveDraftAsync(draft);

        if (result.Cmi5 is not null)
        {
            await _notifier.SuccessAsync(H["The cmi5 course \"{0}\" was imported with {1} AUs. Publish the item to give it to learners.", result.Cmi5.Title, result.Cmi5.Aus.Count]);

            return RedirectToAction("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = item.ContentItemId });
        }

        // Articulate's SCORM Driver runs in several frames, which only the package content host lets reach each other.
        if (result.UsesScormDriver && await _contentHost.GetOriginAsync() is null)
        {
            await _notifier.WarningAsync(H["This package uses Articulate's SCORM Driver (Rise or Storyline 360), which runs in several frames. It shows a blank page until a package content host is set under Settings › Learning packages."]);
        }

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

    private async Task<PackageUploadViewModel> BuildModelAsync(ContentItem item)
        => new()
        {
            ContentItemId = item.ContentItemId,
            Title = item.DisplayText,
            Part = item.TryGet<LearningPackagePart>(out var part) ? part : null,
            MaxUploadMb = _options.MaxUploadBytes / (1024 * 1024),
            AcceptCmi5 = _options.AcceptCmi5,
            HasContentHost = await _contentHost.GetOriginAsync() is not null,
            CanManageSettings = await _authorizationService.AuthorizeAsync(User, LearningPackagesPermissions.ManageLearningPackageSettings),
        };
}
