using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Emails;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The default <see cref="ITemplatedEmailTemplates"/>: asks each <see cref="ITemplatedEmailTemplateSource"/> for the
/// site's template of the kind and its layout, and renders them with restricted Liquid (<see cref="RestrictedLiquid"/>),
/// which reads only the email's documented values: the email's models as plain values, the built HTML as
/// <c>{{ Content }}</c>, the subject as <c>{{ Subject }}</c> and the site's name as <c>{{ SiteName }}</c>, never the
/// request or the site's other objects. A template that doesn't parse or fails to render gives the built email.
/// </summary>
public sealed partial class LiquidTemplatedEmailTemplates : ITemplatedEmailTemplates
{
    private readonly IEnumerable<ITemplatedEmailTemplateSource> _sources;
    private readonly ISiteService _siteService;
    private readonly ILocalClock _localClock;
    private readonly IStringLocalizer _localizer;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidTemplatedEmailTemplates"/> class.
    /// </summary>
    /// <param name="sources">The sources of the site's templates.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public LiquidTemplatedEmailTemplates(
        IEnumerable<ITemplatedEmailTemplateSource> sources,
        ISiteService siteService,
        ILocalClock localClock,
        IStringLocalizer<LiquidTemplatedEmailTemplates> localizer,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _sources = sources;
        _siteService = siteService;
        _localClock = localClock;
        _localizer = localizer;
        _logger = loggerFactory.CreateLogger<LiquidTemplatedEmailTemplates>();
    }

    /// <inheritdoc/>
    public async Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = new TemplatedEmailMessage(context.Subject, context.Text, context.Html);

        if (string.IsNullOrEmpty(context.Kind))
        {
            return built;
        }

        TemplatedEmailOverride found = null;

        foreach (var source in _sources)
        {
            if ((found = await source.FindAsync(context.Kind, cancellationToken)) is not null)
            {
                break;
            }
        }

        var template = found?.Template is { IsEnabled: true } enabled ? enabled : null;
        var layout = found?.Layout;

        if (template is null && string.IsNullOrWhiteSpace(layout))
        {
            return built;
        }

        try
        {
            var site = await _siteService.GetSiteSettingsAsync();
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal)
            {
                ["Content"] = StringValue.Create(context.Html ?? string.Empty, encode: false),
                ["Subject"] = StringValue.Create(context.Subject ?? string.Empty),
                ["SiteName"] = StringValue.Create(site.SiteName ?? string.Empty),
            };

            foreach (var (name, value) in found.Values ?? new Dictionary<string, string>())
            {
                values[name] = StringValue.Create(value ?? string.Empty);
            }

            foreach (var (name, model) in context.Models ?? new Dictionary<string, object>())
            {
                values[name] = LiquidModelValues.Create(model, RestrictedLiquid.Options);
            }

            foreach (var (name, value) in context.Values ?? new Dictionary<string, string>())
            {
                values[name] = StringValue.Create(value ?? string.Empty);
            }

            var subject = context.Subject;
            var html = context.Html;
            var liquid = await RestrictedLiquidContexts.CreateAsync(_localClock, _localizer);

            if (template is not null)
            {
                if (!string.IsNullOrWhiteSpace(template.Subject))
                {
                    subject = (await RenderOrThrowAsync(template.Subject, values, NullEncoder.Default, liquid)).Trim();
                }

                if (!string.IsNullOrWhiteSpace(template.Body))
                {
                    html = await RenderOrThrowAsync(template.Body, values, HtmlEncoder.Default, liquid);
                    values["Content"] = StringValue.Create(html, encode: false);
                }
            }

            // A text-only email stays text-only, unless the site's template wrote a body for it.
            if (!string.IsNullOrWhiteSpace(layout) && !string.IsNullOrEmpty(html))
            {
                html = await RenderOrThrowAsync(layout, values, HtmlEncoder.Default, liquid);
            }

            // The text part follows the HTML a template wrote; with only a layout, the built text stays.
            var text = template is not null && !string.IsNullOrWhiteSpace(template.Body) ? ToText(html) : context.Text;

            return new TemplatedEmailMessage(string.IsNullOrWhiteSpace(subject) ? context.Subject : subject, text, html);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The site's template of the '{Kind}' email couldn't render; the built email was sent.", context.Kind);

            return built;
        }
    }

    /// <summary>
    /// Checks that a template of an email parses, for its editor and its import.
    /// </summary>
    /// <param name="source">The subject, body or layout.</param>
    /// <param name="error">Why it doesn't parse, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it parses.</returns>
    public static bool TryValidate(string source, out string error)
        => RestrictedLiquid.TryValidate(source, out error);

    // A part that doesn't parse sends the built email instead, never the parser's message.
    private static async Task<string> RenderOrThrowAsync(string source, Dictionary<string, FluidValue> values, TextEncoder encoder, RestrictedLiquidContext context)
        => await RestrictedLiquid.RenderAsync(source, values, encoder, context)
            ?? throw new InvalidOperationException("The template doesn't parse, or uses a filter that isn't available.");

    /// <summary>
    /// Writes the text part of an HTML body.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The text.</returns>
    public static string ToText(string html)
        => System.Net.WebUtility.HtmlDecode(Tags().Replace(Breaks().Replace(html ?? string.Empty, "\n"), string.Empty)).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</h[1-6]>|</li>|</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();
}
