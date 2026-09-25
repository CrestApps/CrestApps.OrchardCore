using System.Net;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// A harness page carrying the soft phone's real markup (<c>?widget</c>), read from <c>Views/SoftPhoneWidget.cshtml</c>
/// with its Razor stripped, so a test can see and measure what an agent sees: the real classes, icons and labels
/// rather than the bare stand-in the other harness pages use.
/// </summary>
/// <remarks>
/// <para>
/// With <c>?standalone</c> the phone is wrapped as the <c>/softphone</c> page wraps it for the desktop app and the
/// browser extension, with that page's own styles; otherwise it floats in the corner as it does on a site page. With
/// <c>?dark</c> the page is in the dark theme. With <c>?host</c> the page also loads the UI framework a real page
/// carries (Bootstrap and Font Awesome), from the CDN, for screenshots; the tests that measure layout do not need it.
/// </para>
/// <para>
/// The view is read as it is, so a change to it shows here; a Razor construct this does not know how to strip makes
/// the page refuse to build rather than render something the real view does not.
/// </para>
/// </remarks>
public static partial class SoftPhoneWidgetPage
{
    private const string BootstrapUrl = "https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css";
    private const string FontAwesomeUrl = "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.7.2/css/all.min.css";

    /// <summary>
    /// Builds the page.
    /// </summary>
    /// <param name="configJson">The widget's configuration.</param>
    /// <param name="scripts">The script tags to load.</param>
    /// <param name="stylesheetUrl">The soft phone's stylesheet.</param>
    /// <param name="standalone">Whether to wrap the phone as the standalone page does.</param>
    /// <param name="dark">Whether the page is in the dark theme.</param>
    /// <param name="host">Whether to load the host page's UI framework from the CDN.</param>
    public static string Build(string configJson, string scripts, string stylesheetUrl, bool standalone, bool dark, bool host)
    {
        var markup = RenderWidgetMarkup(configJson);
        // Without the CDN, the two rules of Bootstrap's reboot the widget's layout relies on: borders sit inside an
        // element's width, and the hidden attribute hides whatever display a class gives the element.
        var frameworks = host
            ? $"<link rel=\"stylesheet\" href=\"{BootstrapUrl}\" /><link rel=\"stylesheet\" href=\"{FontAwesomeUrl}\" />"
            : "<style>*, *::before, *::after { box-sizing: border-box; } [hidden] { display: none !important; }</style>";
        var standaloneStyles = standalone ? ReadStandaloneStyles() : string.Empty;
        var body = standalone
            ? $"<div class=\"softphone-standalone\" data-softphone-embedded=\"true\" data-softphone-answer-call-id=\"\">{markup}</div>"
            : markup;

        return $$"""
        <!DOCTYPE html>
        <html lang="en"{{(dark ? " data-bs-theme=\"dark\"" : string.Empty)}}>
        <head>
            <meta charset="utf-8" />
            <title>Soft Phone Widget Test</title>
            {{frameworks}}
            <link rel="stylesheet" href="{{stylesheetUrl}}" />
            {{(standalone ? standaloneStyles : $"<style>body {{ background: {(dark ? "#121417" : "#f4f5f7")}; }}</style>")}}
        </head>
        <body>
            {{body}}
            {{scripts}}
        </body>
        </html>
        """;
    }

    /// <summary>
    /// The widget's markup, from the view, with its Razor stripped.
    /// </summary>
    /// <param name="configJson">The configuration the markup carries.</param>
    public static string RenderWidgetMarkup(string configJson)
    {
        var source = File.ReadAllText(Path.Combine(SoftPhoneAssets.ModuleDirectory, "Views", "SoftPhoneWidget.cshtml"));
        var start = source.IndexOf("<div id=\"telephony-soft-phone\"", StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException("SoftPhoneWidget.cshtml no longer carries the widget's root element.");
        }

        var markup = source[start..];
        markup = RazorComment().Replace(markup, string.Empty);
        markup = DisplayBlock().Replace(markup, string.Empty);
        markup = KeypadLoop().Replace(markup, match =>
        {
            var keys = Regex.Matches(match.Groups["keys"].Value, "\"([^\"]+)\"").Select(key => key.Groups[1].Value);
            var body = match.Groups["body"].Value;

            return string.Join(Environment.NewLine, keys.Select(key => body.Replace("@key", WebUtility.HtmlEncode(key), StringComparison.Ordinal)));
        });
        markup = Localized().Replace(markup, match => WebUtility.HtmlEncode(Regex.Unescape(match.Groups["text"].Value)));
        markup = markup.Replace("@accentColor", "#2f6fed", StringComparison.Ordinal);
        markup = Config().Replace(markup, $"data-config=\"{WebUtility.HtmlEncode(configJson)}\"");

        if (markup.Contains('@'))
        {
            var at = markup.IndexOf('@');

            throw new InvalidOperationException(
                $"SoftPhoneWidget.cshtml carries Razor the harness does not strip: {markup[at..Math.Min(at + 80, markup.Length)]}");
        }

        return markup;
    }

    // The standalone page's own <style> block, which expands the phone to fill the window.
    private static string ReadStandaloneStyles()
    {
        var source = File.ReadAllText(Path.Combine(SoftPhoneAssets.ModuleDirectory, "Views", "SoftPhone", "Index.cshtml"));
        var match = StyleBlock().Match(source);

        if (!match.Success)
        {
            throw new InvalidOperationException("Views/SoftPhone/Index.cshtml no longer carries its own styles.");
        }

        return match.Value;
    }

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"@if \(Model\.\w+ != null\)\s*\{\s*@await DisplayAsync\(Model\.\w+\)\s*\}", RegexOptions.Singleline)]
    private static partial Regex DisplayBlock();

    [GeneratedRegex(@"@foreach \(var key in new\[\] \{(?<keys>[^}]*)\}\)\s*\{\s*(?<body>.*?)\s*\}", RegexOptions.Singleline)]
    private static partial Regex KeypadLoop();

    [GeneratedRegex(@"@T\[""(?<text>(?:[^""\\]|\\.)*)""\]")]
    private static partial Regex Localized();

    [GeneratedRegex(@"data-config=""@JsonSerializer\.Serialize\([^""]*\)""")]
    private static partial Regex Config();

    [GeneratedRegex(@"<style>.*?</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();
}
