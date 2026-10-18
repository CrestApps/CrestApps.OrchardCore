using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Downloads.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Downloads.Drivers;

/// <summary>
/// The files a product delivers, uploaded straight to the private store from the product's editor.
/// </summary>
public sealed class DigitalFilesPartDisplayDriver : ContentPartDisplayDriver<DigitalFilesPart>
{
    private const int MaxFiles = 50;

    private readonly IDigitalFileStore _fileStore;
    private readonly ISiteService _siteService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public DigitalFilesPartDisplayDriver(
        IDigitalFileStore fileStore,
        ISiteService siteService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<DigitalFilesPartDisplayDriver> stringLocalizer)
    {
        _fileStore = fileStore;
        _siteService = siteService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(DigitalFilesPart part, BuildPartEditorContext context)
        => Initialize<DigitalFilesPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var httpContext = _httpContextAccessor.HttpContext;

            model.Files = [.. (part.Files ?? []).Select(file => new DigitalFileViewModel { FileId = file.FileId, FileName = file.FileName, Size = file.Size })];
            model.DownloadLimit = part.DownloadLimit;
            model.LinkLifetimeDays = part.LinkLifetimeDays;
            model.Defaults = await _siteService.GetSettingsAsync<DownloadSettings>();
            model.UploadUrl = httpContext is null ? null : _linkGenerator.GetPathByAction(httpContext, "Upload", "DigitalFilesAdmin", new { area = DownloadsConstants.Feature });
            model.FileUrlFormat = httpContext is null ? null : _linkGenerator.GetPathByAction(httpContext, "File", "DigitalFilesAdmin", new { area = DownloadsConstants.Feature, fileId = "__id__" });
        });

    public override async Task<IDisplayResult> UpdateAsync(DigitalFilesPart part, UpdatePartEditorContext context)
    {
        var model = new DigitalFilesPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var files = new List<DigitalFile>();

        // Only files the private store has are kept: an id typed into the form can't point anywhere else, and the
        // name and size come from the store, not the form.
        foreach (var posted in (model.Files ?? []).Where(file => !file.Remove).Take(MaxFiles))
        {
            if (files.Any(file => file.FileId == posted.FileId) || await _fileStore.FindAsync(posted.FileId) is not { } stored)
            {
                continue;
            }

            files.Add(new DigitalFile { FileId = stored.FileId, FileName = stored.FileName, Size = stored.Size });
        }

        if (model.DownloadLimit is < 0 or > 10_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DownloadLimit), S["Enter a number of downloads from 0 to 10000, or leave it blank."]);
        }

        if (model.LinkLifetimeDays is < 0 or > 3650)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.LinkLifetimeDays), S["Enter a number of days from 0 to 3650, or leave it blank."]);
        }

        part.Files = files;
        part.DownloadLimit = model.DownloadLimit;
        part.LinkLifetimeDays = model.LinkLifetimeDays;

        return Edit(part, context);
    }
}
