using System.Net;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Endpoints;

/// <summary>
/// The unsubscribe link of bulk and outreach email. Opening it shows a page with one button, so a mail scanner that
/// follows links cannot unsubscribe anyone; posting it (the button, or a mailbox provider's one-click unsubscribe per
/// RFC 8058) marks every contact holding the address <c>Do not email</c>. The signed token is the only credential.
/// </summary>
internal static class EmailUnsubscribeEndpoint
{
    public static IEndpointRouteBuilder AddEmailUnsubscribeEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet(EmailChannelConstants.UnsubscribeRoute, ShowAsync)
            .AllowAnonymous();

        _ = builder.MapPost(EmailChannelConstants.UnsubscribeRoute, UnsubscribeAsync)
            .DisableAntiforgery()
            .AllowAnonymous();

        return builder;
    }

    private static IResult ShowAsync(
        string token,
        IEmailUnsubscribeLinks links,
        IStringLocalizer<EmailUnsubscribeLinks> localizer)
    {
        if (!links.TryRead(token, out var contactAddress, out _))
        {
            return Page(localizer["Unsubscribe"].Value, localizer["This unsubscribe link is not valid."].Value, form: null);
        }

        var form = $"""
            <form method="post">
                <button type="submit">{WebUtility.HtmlEncode(localizer["Unsubscribe"].Value)}</button>
            </form>
            """;

        return Page(
            localizer["Unsubscribe"].Value,
            localizer["Stop sending email to {0}?", contactAddress].Value,
            form);
    }

    private static async Task<IResult> UnsubscribeAsync(
        string token,
        IEmailUnsubscribeLinks links,
        IEmailOptOutService optOutService,
        IStringLocalizer<EmailUnsubscribeLinks> localizer,
        HttpContext httpContext)
    {
        if (!links.TryRead(token, out var contactAddress, out _))
        {
            return Page(localizer["Unsubscribe"].Value, localizer["This unsubscribe link is not valid."].Value, form: null, StatusCodes.Status400BadRequest);
        }

        await optOutService.OptOutAsync(contactAddress, httpContext.RequestAborted);

        return Page(localizer["Unsubscribed"].Value, localizer["You will not receive these emails any more."].Value, form: null);
    }

    private static IResult Page(string title, string message, string form, int statusCode = StatusCodes.Status200OK)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <meta name="robots" content="noindex">
                <title>{{WebUtility.HtmlEncode(title)}}</title>
                <style>
                    body { font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #f6f8fa; color: #1f2328; margin: 0; }
                    main { max-width: 28rem; margin: 15vh auto; background: #fff; border: 1px solid #d1d9e0; border-radius: 8px; padding: 2rem; text-align: center; }
                    button { font: inherit; background: #1f2328; color: #fff; border: 0; border-radius: 6px; padding: .6rem 1.4rem; cursor: pointer; }
                </style>
            </head>
            <body>
                <main>
                    <h1 style="font-size: 1.25rem;">{{WebUtility.HtmlEncode(title)}}</h1>
                    <p>{{WebUtility.HtmlEncode(message)}}</p>
                    {{form}}
                </main>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);
    }
}
