using System.Text.Encodings.Web;
using Shortcodes;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The <c>[survey name="..." collector="..." display="button|link"]...[/survey]</c> shortcode: a button or a link to a
/// survey's public link. The content between the tags, when there is any, is the text.
/// </summary>
internal sealed class SurveyShortcodeProvider : IShortcodeProvider
{
    private readonly SurveyEmbedResolver _resolver;
    private readonly HtmlEncoder _htmlEncoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyShortcodeProvider"/> class.
    /// </summary>
    /// <param name="resolver">The embed resolver.</param>
    /// <param name="htmlEncoder">The HTML encoder.</param>
    public SurveyShortcodeProvider(
        SurveyEmbedResolver resolver,
        HtmlEncoder htmlEncoder)
    {
        _resolver = resolver;
        _htmlEncoder = htmlEncoder;
    }

    /// <inheritdoc />
    public async ValueTask<string> EvaluateAsync(string identifier, Arguments arguments, string content, Context context)
    {
        if (!string.Equals(identifier, "survey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var (survey, version, collector) = await _resolver.ResolveAsync(arguments.Named("name") ?? arguments.At(0), arguments.Named("collector"));

        if (survey is null || version is null || collector is null)
        {
            return string.Empty;
        }

        var text = string.IsNullOrWhiteSpace(content)
            ? (string.IsNullOrWhiteSpace(version.Definition?.Title) ? survey.Title : version.Definition.Title)
            : content.Trim();
        var url = "/s/" + Uri.EscapeDataString(collector.Token);
        var css = string.Equals(arguments.Named("display"), "link", StringComparison.OrdinalIgnoreCase) ? "survey-embed-link" : "btn btn-primary survey-embed-button";

        return $"<a href=\"{_htmlEncoder.Encode(url)}\" class=\"{css}\">{_htmlEncoder.Encode(text ?? string.Empty)}</a>";
    }
}
