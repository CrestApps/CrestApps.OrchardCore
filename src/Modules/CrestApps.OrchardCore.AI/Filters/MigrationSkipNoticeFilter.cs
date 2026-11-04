using System.Globalization;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Migrations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.AI.Filters;

/// <summary>
/// Shows the people who manage AI profiles a warning on admin pages while data migrations have skipped records that nobody has looked at,
/// with a button that dismisses it. The skips are kept by <see cref="IMigrationSkipLog"/>.
/// </summary>
public sealed class MigrationSkipNoticeFilter : IAsyncResultFilter
{
    private readonly IMigrationSkipLog _skipLog;
    private readonly INotifier _notifier;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAntiforgery _antiforgery;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrationSkipNoticeFilter"/> class.
    /// </summary>
    /// <param name="skipLog">The log of migration skips.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="antiforgery">The antiforgery service, for the dismiss button.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public MigrationSkipNoticeFilter(
        IMigrationSkipLog skipLog,
        INotifier notifier,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        LinkGenerator linkGenerator,
        IHtmlLocalizer<MigrationSkipNoticeFilter> htmlLocalizer)
    {
        _skipLog = skipLog;
        _notifier = notifier;
        _authorizationService = authorizationService;
        _antiforgery = antiforgery;
        _linkGenerator = linkGenerator;
        H = htmlLocalizer;
    }

    /// <inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var httpContext = context.HttpContext;

        if (context.Result is ViewResult &&
            AdminAttribute.IsApplied(httpContext) &&
            httpContext.User?.Identity?.IsAuthenticated == true &&
            await _authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.ManageAIProfiles))
        {
            var entries = await _skipLog.ListAsync(httpContext.RequestAborted);

            if (entries.Count > 0)
            {
                var migrations = string.Join(", ", entries
                    .GroupBy(entry => entry.MigrationName, StringComparer.Ordinal)
                    .Select(group => string.Create(CultureInfo.CurrentCulture, $"{group.Key} ({group.Sum(entry => entry.SkippedCount)})")));

                await _notifier.WarningAsync(H["Data migrations skipped records they could not migrate: {0}. The log lists each record and why. {1}", migrations, DismissButton(httpContext)]);
            }
        }

        await next();
    }

    private HtmlContentBuilder DismissButton(HttpContext httpContext)
    {
        var action = _linkGenerator.GetPathByAction(httpContext, "Dismiss", "MigrationNotices", new { area = AIConstants.Feature.Area });
        var token = _antiforgery.GetAndStoreTokens(httpContext).RequestToken;
        var returnUrl = httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString;

        var builder = new HtmlContentBuilder();

        builder.AppendHtml("<form method=\"post\" class=\"d-inline\" action=\"");
        builder.Append(action);
        builder.AppendHtml("\"><input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"");
        builder.Append(token);
        builder.AppendHtml("\" /><input type=\"hidden\" name=\"returnUrl\" value=\"");
        builder.Append(returnUrl);
        builder.AppendHtml("\" /><button type=\"submit\" class=\"btn btn-sm btn-link p-0 align-baseline\">");
        builder.AppendHtml(H["Dismiss"]);
        builder.AppendHtml("</button></form>");

        return builder;
    }
}
