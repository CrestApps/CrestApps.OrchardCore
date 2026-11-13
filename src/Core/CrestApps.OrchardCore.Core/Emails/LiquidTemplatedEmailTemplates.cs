using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Emails;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The default <see cref="ITemplatedEmailTemplates"/>: asks each <see cref="ITemplatedEmailTemplateSource"/> for the
/// site's template of the kind and its layout, and renders them with Liquid. The email's models are read as plain
/// values, the built HTML is <c>{{ Content }}</c>, the subject <c>{{ Subject }}</c> and the site's name
/// <c>{{ SiteName }}</c>. A template that fails to render gives the built email.
/// </summary>
public sealed partial class LiquidTemplatedEmailTemplates : ITemplatedEmailTemplates
{
    private static readonly TemplateOptions _options = TemplateOptions.Default;

    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<ITemplatedEmailTemplateSource> _sources;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidTemplatedEmailTemplates"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the Liquid template manager when the Liquid feature is on.</param>
    /// <param name="sources">The sources of the site's templates.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="logger">The logger.</param>
    public LiquidTemplatedEmailTemplates(
        IServiceProvider serviceProvider,
        IEnumerable<ITemplatedEmailTemplateSource> sources,
        ISiteService siteService,
        ILogger<LiquidTemplatedEmailTemplates> logger)
        : this(serviceProvider, sources, siteService, (ILogger)logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidTemplatedEmailTemplates"/> class that logs to another
    /// category, for a module's own templates built on these.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the Liquid template manager when the Liquid feature is on.</param>
    /// <param name="sources">The sources of the site's templates.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="logger">The logger.</param>
    public LiquidTemplatedEmailTemplates(
        IServiceProvider serviceProvider,
        IEnumerable<ITemplatedEmailTemplateSource> sources,
        ISiteService siteService,
        ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _sources = sources;
        _siteService = siteService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = new TemplatedEmailMessage(context.Subject, context.Text, context.Html);

        if (string.IsNullOrEmpty(context.Kind) || _serviceProvider.GetService<ILiquidTemplateManager>() is not { } liquid)
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
                values[name] = LiquidModelValues.Create(model, _options);
            }

            foreach (var (name, value) in context.Values ?? new Dictionary<string, string>())
            {
                values[name] = StringValue.Create(value ?? string.Empty);
            }

            var subject = context.Subject;
            var html = context.Html;

            if (template is not null)
            {
                if (!string.IsNullOrWhiteSpace(template.Subject))
                {
                    subject = (await liquid.RenderStringAsync(template.Subject, NullEncoder.Default, values)).Trim();
                }

                if (!string.IsNullOrWhiteSpace(template.Body))
                {
                    html = await liquid.RenderStringAsync(template.Body, HtmlEncoder.Default, values);
                    values["Content"] = StringValue.Create(html, encode: false);
                }
            }

            // A text-only email stays text-only, unless the site's template wrote a body for it.
            if (!string.IsNullOrWhiteSpace(layout) && !string.IsNullOrEmpty(html))
            {
                html = await liquid.RenderStringAsync(layout, HtmlEncoder.Default, values);
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
