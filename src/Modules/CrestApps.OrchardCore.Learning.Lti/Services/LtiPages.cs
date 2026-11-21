using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Learning.Lti.Services;

/// <summary>
/// The plain pages of the LTI flows, which a tool's site posts to or which post to a tool's site, so they do not depend
/// on the sign-in cookie or the theme: an auto-posting form, and a message with a link back.
/// </summary>
internal static class LtiPages
{
    /// <summary>
    /// Writes a page that posts fields to an address as soon as it loads, with a button for browsers without script.
    /// </summary>
    public static ContentResult FormPost(ControllerBase controller, string action, IReadOnlyDictionary<string, string> fields, string message, string button)
    {
        var encoder = HtmlEncoder.Default;
        var html = new StringBuilder();

        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>")
            .Append(encoder.Encode(message))
            .Append("</title></head><body><form method=\"post\" action=\"")
            .Append(encoder.Encode(action))
            .Append("\"><p>")
            .Append(encoder.Encode(message))
            .Append("</p>");

        foreach (var (name, value) in fields)
        {
            html.Append("<input type=\"hidden\" name=\"")
                .Append(encoder.Encode(name))
                .Append("\" value=\"")
                .Append(encoder.Encode(value ?? string.Empty))
                .Append("\">");
        }

        html.Append("<noscript><button type=\"submit\">")
            .Append(encoder.Encode(button))
            .Append("</button></noscript></form><script>document.forms[0].submit();</script></body></html>");

        return Page(controller, 200, html.ToString());
    }

    /// <summary>
    /// Writes a page with a title, lines of text and an optional link.
    /// </summary>
    public static ContentResult Message(ControllerBase controller, int statusCode, string title, IEnumerable<string> lines, string linkUrl, string linkText)
    {
        var encoder = HtmlEncoder.Default;
        var html = new StringBuilder();

        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>")
            .Append(encoder.Encode(title))
            .Append("</title><style>body{font-family:system-ui,sans-serif;max-width:40rem;margin:2rem auto;padding:0 1rem;line-height:1.5}</style></head><body><h1>")
            .Append(encoder.Encode(title))
            .Append("</h1>");

        foreach (var line in lines ?? [])
        {
            html.Append("<p>").Append(encoder.Encode(line)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(linkUrl) && Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            html.Append("<p><a href=\"").Append(encoder.Encode(linkUrl)).Append("\">").Append(encoder.Encode(linkText)).Append("</a></p>");
        }

        html.Append("</body></html>");

        return Page(controller, statusCode, html.ToString());
    }

    private static ContentResult Page(ControllerBase controller, int statusCode, string html)
    {
        controller.Response.Headers.CacheControl = "no-store";
        controller.Response.Headers["Referrer-Policy"] = "no-referrer";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html; charset=utf-8",
            StatusCode = statusCode,
        };
    }
}
