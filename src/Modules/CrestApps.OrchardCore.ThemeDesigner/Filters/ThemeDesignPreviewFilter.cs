using System.Text.Encodings.Web;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using CrestApps.OrchardCore.ThemeDesigner.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ThemeDesigner.Filters;

/// <summary>
/// Shows a small bar on the pages a designer sees with their theme draft, with links to review the draft and to stop
/// the preview. It goes with the page's scripts, which every theme outputs, so it shows whatever the draft does to the
/// layout.
/// </summary>
internal sealed class ThemeDesignPreviewFilter : IAsyncResultFilter
{
    private readonly ThemeDesignPreview _preview;
    private readonly IResourceManager _resourceManager;
    private readonly IAntiforgery _antiforgery;
    private readonly LinkGenerator _linkGenerator;
    private readonly HtmlEncoder _htmlEncoder;

    internal readonly IStringLocalizer S;

    public ThemeDesignPreviewFilter(
        ThemeDesignPreview preview,
        IResourceManager resourceManager,
        IAntiforgery antiforgery,
        LinkGenerator linkGenerator,
        HtmlEncoder htmlEncoder,
        IStringLocalizer<ThemeDesignPreviewFilter> stringLocalizer)
    {
        _preview = preview;
        _resourceManager = resourceManager;
        _antiforgery = antiforgery;
        _linkGenerator = linkGenerator;
        _htmlEncoder = htmlEncoder;
        S = stringLocalizer;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ViewResult or PageResult &&
            await _preview.GetDraftAsync() is { } draft &&
            (!AdminAttribute.IsApplied(context.HttpContext) || ChangesTheAdmin(draft)))
        {
            var httpContext = context.HttpContext;
            var routeValues = new { area = ThemeDesignerConstants.Features.ThemeDesigner };
            var reviewUrl = _linkGenerator.GetPathByAction(httpContext, "Index", "ThemeDesigner", routeValues);
            var previewUrl = _linkGenerator.GetPathByAction(httpContext, "Preview", "ThemeDesigner", routeValues);
            var returnUrl = httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString;
            var token = _antiforgery.GetAndStoreTokens(httpContext);

            var html = $"""
                <div role="status" data-theme-draft-bar style="position:fixed;left:1rem;bottom:1rem;z-index:2147483000;display:flex;align-items:center;gap:.75rem;padding:.5rem .75rem;border-radius:.5rem;background:#1f2937;color:#f9fafb;font:500 13px/1.4 system-ui,-apple-system,'Segoe UI',sans-serif;box-shadow:0 4px 16px rgba(0,0,0,.25);">
                    <span>{Encode(S["Previewing your theme draft ({0} changes)", draft.ChangeCount])}</span>
                    <a href="{Encode(reviewUrl)}" style="color:#93c5fd;text-decoration:underline;">{Encode(S["Review"])}</a>
                    <form method="post" action="{Encode(previewUrl)}" style="margin:0;">
                        <input type="hidden" name="{Encode(token.FormFieldName)}" value="{Encode(token.RequestToken)}" />
                        <input type="hidden" name="preview" value="false" />
                        <input type="hidden" name="returnUrl" value="{Encode(returnUrl)}" />
                        <button type="submit" style="all:unset;cursor:pointer;color:#93c5fd;text-decoration:underline;">{Encode(S["Stop preview"])}</button>
                    </form>
                </div>
                """;

            _resourceManager.RegisterFootScript(new HtmlString(html));
        }

        await next();
    }

    // The admin shows the draft's admin templates, and its resources offered to the admin; the rest of the draft
    // only changes the front end, so admin pages don't announce it.
    internal static bool ChangesTheAdmin(ThemeDesignDraft draft)
        => draft.Templates.Any(template => template.Scope == ThemeDesignScope.Admin) ||
            draft.Resources.Any(change => change.Removed || change.Resource?.Target is SiteResourceTarget.Admin or SiteResourceTarget.Everywhere);

    private string Encode(string value)
        => _htmlEncoder.Encode(value ?? string.Empty);
}
