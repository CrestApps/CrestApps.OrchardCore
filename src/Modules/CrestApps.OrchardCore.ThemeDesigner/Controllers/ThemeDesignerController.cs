using CrestApps.OrchardCore.SiteResources;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using CrestApps.OrchardCore.ThemeDesigner.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.ThemeDesigner.Controllers;

/// <summary>
/// Design › Theme Draft: the signed-in designer's unpublished changes, to review, revert, publish or discard. The page
/// always shows the published admin, so a draft that breaks the admin can still be discarded here.
/// </summary>
[Admin]
public sealed class ThemeDesignerController : Controller
{
    private readonly IThemeDesignDraftService _draftService;
    private readonly IThemeShapeExplorer _shapeExplorer;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public ThemeDesignerController(
        IThemeDesignDraftService draftService,
        IThemeShapeExplorer shapeExplorer,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ThemeDesignerController> htmlLocalizer)
    {
        _draftService = draftService;
        _shapeExplorer = shapeExplorer;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [Admin("theme-designer", "ThemeDesignerIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var draft = await _draftService.GetDraftAsync();
        var changes = await _draftService.GetChangesAsync();

        return View(new ThemeDesignerIndexViewModel
        {
            Draft = draft,
            Themes = await _shapeExplorer.GetActiveThemesAsync(),
            List = new AdminListContext<ThemeDesignChange>
            {
                Source = changes,
            },
        });
    }

    [Admin("theme-designer/change", "ThemeDesignerChange")]
    public async Task<IActionResult> Change(ThemeDesignChangeKind kind, string name)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var model = new ThemeDesignChangeViewModel
        {
            Kind = kind,
            Name = name,
        };

        if (kind == ThemeDesignChangeKind.SiteResource)
        {
            var details = await _draftService.GetResourceAsync(name);

            if (details is null)
            {
                return NotFound();
            }

            model.Status = details.DraftStatus;
            model.PublishedContent = details.Published?.Content;
            model.DraftContent = details.Draft?.Content;
        }
        else
        {
            var details = await _draftService.GetTemplateAsync(kind == ThemeDesignChangeKind.AdminTemplate ? ThemeDesignScope.Admin : ThemeDesignScope.Site, name);

            if (details is null)
            {
                return NotFound();
            }

            model.Status = details.DraftStatus;
            model.PublishedContent = details.PublishedContent;
            model.DraftContent = details.DraftContent;
        }

        return View(model);
    }

    [HttpPost]
    [Admin("theme-designer/revert", "ThemeDesignerRevert")]
    public async Task<IActionResult> Revert(ThemeDesignChangeKind kind, string name)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var result = await _draftService.RevertAsync(kind, name);

        await NotifyAsync(result, H["The change has been dropped from your draft."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("theme-designer/publish", "ThemeDesignerPublish")]
    public async Task<IActionResult> Publish()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var result = await _draftService.PublishAsync();

        await NotifyAsync(result, H["Your draft has been published. Everyone sees it now."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("theme-designer/discard", "ThemeDesignerDiscard")]
    public async Task<IActionResult> Discard()
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var result = await _draftService.DiscardAsync();

        await NotifyAsync(result, H["Your draft has been discarded."]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Admin("theme-designer/preview", "ThemeDesignerPreview")]
    public async Task<IActionResult> Preview(bool preview, string returnUrl)
    {
        if (!await CanDesignAsync())
        {
            return Forbid();
        }

        var result = await _draftService.SetPreviewAsync(preview);

        await NotifyAsync(result, preview
            ? H["You see your draft while you browse the site."]
            : H["You see the published site while you browse. Your draft is kept."]);

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    private async Task NotifyAsync(ThemeDesignResult result, LocalizedHtmlString success)
    {
        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(success);

            return;
        }

        foreach (var error in result.Errors)
        {
            await _notifier.ErrorAsync(H["{0}", error]);
        }
    }

    private async Task<bool> CanDesignAsync()
        => await _authorizationService.AuthorizeAsync(User, ThemeDesignerPermissions.ManageTemplates) ||
            await _authorizationService.AuthorizeAsync(User, ThemeDesignerPermissions.ManageAdminTemplates) ||
            await _authorizationService.AuthorizeAsync(User, SiteResourcesPermissions.ManageSiteResources);
}
