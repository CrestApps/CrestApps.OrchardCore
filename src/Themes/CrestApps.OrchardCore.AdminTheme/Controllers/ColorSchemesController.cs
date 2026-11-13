using System.Text;
using CrestApps.OrchardCore.AdminTheme.ColorSchemes;
using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Services;
using CrestApps.OrchardCore.AdminTheme.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.AdminTheme.Controllers;

/// <summary>
/// Lists, adds, edits and deletes the color schemes of the admin (the theme's defaults included), and previews a
/// scheme while it is edited.
/// </summary>
[Admin("AdminTheme/ColorSchemes/{action}/{id?}", "AdminThemeColorSchemes{action}")]
public sealed class ColorSchemesController : Controller
{
    /// <summary>
    /// The id the editor's previews carry; its rules are the ones <see cref="Preview"/> returns.
    /// </summary>
    public const string PreviewSchemeId = "editor-preview";

    private readonly IAuthorizationService _authorizationService;
    private readonly AdminColorSchemeStore _store;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorSchemesController"/> class.
    /// </summary>
    public ColorSchemesController(
        IAuthorizationService authorizationService,
        AdminColorSchemeStore store,
        INotifier notifier,
        IHtmlLocalizer<ColorSchemesController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _store = store;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        return View(new AdminColorSchemesIndexViewModel
        {
            List = new AdminListContext<AdminColorScheme>
            {
                Source = [.. await _store.ListAsync()],
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        return View("Edit", new AdminColorSchemeViewModel { Position = await _store.NextPositionAsync() });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(AdminColorSchemeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        var scheme = ToScheme(model, _store.NewId());
        await _store.SaveAsync(scheme);
        await _notifier.SuccessAsync(H["The color scheme <strong>{0}</strong> was added.", scheme.Name]);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var scheme = await _store.FindAsync(id);

        if (scheme is null)
        {
            return NotFound();
        }

        return View(new AdminColorSchemeViewModel
        {
            Id = scheme.Id,
            Name = scheme.Name,
            Position = scheme.Position,
            Primary = scheme.Primary,
            Deep = scheme.Deep,
            Ink = scheme.Ink,
            Accent = scheme.Accent,
            LightMenu = scheme.LightMenu,
            Hue = scheme.Hue,
            Saturation = scheme.Saturation,
            OnPrimary = scheme.OnPrimary,
            Page = scheme.Page,
            Toolbar = scheme.Toolbar,
            Tint = scheme.Tint,
            TintDark = scheme.TintDark,
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string id, AdminColorSchemeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var existing = await _store.FindAsync(id);

        if (existing is null)
        {
            return NotFound();
        }

        model.Id = existing.Id;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // The cached document is immutable: a new record replaces the stored one.
        var scheme = ToScheme(model, existing.Id);
        await _store.SaveAsync(scheme);
        await _notifier.SuccessAsync(H["The color scheme <strong>{0}</strong> was saved.", scheme.Name]);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var scheme = await _store.FindAsync(id);

        if (scheme is null || !await _store.DeleteAsync(id))
        {
            return NotFound();
        }

        // Users who picked it, and the site default if it was this scheme, fall back on their own: a scheme that is
        // not there is ignored when the page is rendered.
        await _notifier.SuccessAsync(H["The color scheme <strong>{0}</strong> was deleted.", scheme.Name]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The CSS of a scheme being edited, for the editor's previews: the same derivation, contrast fixes included, as a
    /// saved scheme. It takes the editor's fields from the query string.
    /// </summary>
    public async Task<IActionResult> Preview(AdminColorSchemeViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AdminPermissions.ManageAdminSettings))
        {
            return Forbid();
        }

        var definition = AdminColorSchemeStore.ToDefinition(ToScheme(model, PreviewSchemeId));

        if (definition is null)
        {
            return BadRequest();
        }

        var css = new StringBuilder();
        AdminColorSchemeCssBuilder.Append(css, PreviewSchemeId, definition);

        return Content(css.ToString(), "text/css; charset=utf-8");
    }

    private static AdminColorScheme ToScheme(AdminColorSchemeViewModel model, string id)
        => new()
        {
            Id = id,
            Name = model.Name?.Trim(),
            Position = model.Position,
            Primary = model.Primary?.ToLowerInvariant(),
            Deep = model.Deep?.ToLowerInvariant(),
            Ink = model.Ink?.ToLowerInvariant(),
            Accent = model.Accent?.ToLowerInvariant(),
            LightMenu = model.LightMenu,
            Hue = model.Hue,
            Saturation = model.Saturation,
            OnPrimary = Optional(model.OnPrimary),
            Page = Optional(model.Page),
            Toolbar = Optional(model.Toolbar),
            Tint = Optional(model.Tint),
            TintDark = Optional(model.TintDark),
        };

    private static string Optional(string color)
        => string.IsNullOrWhiteSpace(color) ? null : color.Trim().ToLowerInvariant();
}
