using System.Security.Claims;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Works out, once per request, whether the page is rendered with the viewer's theme draft.
/// </summary>
internal sealed class ThemeDesignPreview
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ThemeDesignDraftStore _draftStore;

    private Task<ThemeDesignDraft> _draft;

    public ThemeDesignPreview(
        IHttpContextAccessor httpContextAccessor,
        ThemeDesignDraftStore draftStore)
    {
        _httpContextAccessor = httpContextAccessor;
        _draftStore = draftStore;
    }

    /// <summary>
    /// Gets the draft the page is rendered with, to read only, or <see langword="null"/> when it is rendered as
    /// published: the viewer isn't signed in, has no draft, turned its preview off, or is on the Theme Designer's own
    /// page, which always shows the published admin so a broken draft can still be discarded.
    /// </summary>
    public Task<ThemeDesignDraft> GetDraftAsync()
        => _draft ??= ResolveAsync();

    private async Task<ThemeDesignDraft> ResolveAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true || IsThemeDesignerPage(httpContext))
        {
            return null;
        }

        var draft = await _draftStore.GetAsync(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier));

        return draft is { Preview: true } && draft.ChangeCount > 0
            ? draft
            : null;
    }

    private static bool IsThemeDesignerPage(HttpContext httpContext)
        => string.Equals(httpContext.GetRouteValue("area") as string, ThemeDesignerConstants.Features.ThemeDesigner, StringComparison.OrdinalIgnoreCase);
}
