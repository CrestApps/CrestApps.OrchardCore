using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Commerce.Emails;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// The store's Liquid templates of its emails: an email with a template that's on is rendered from it, with the built
/// email as <c>{{ Content }}</c>, its records by name (such as <c>{{ Order }}</c>) and the email's own values; the layout
/// goes around every email with a kind. A template that fails to render sends the built email instead, so a typo never
/// stops an email. Without the Liquid feature, every email goes as built.
/// </summary>
public sealed partial class LiquidCommerceEmailTemplates : ICommerceEmailTemplates
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };
    private static readonly TemplateOptions _options = TemplateOptions.Default;

    private readonly IServiceProvider _serviceProvider;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidCommerceEmailTemplates"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, for the Liquid template manager, which is there only when the
    /// Liquid feature is on.</param>
    /// <param name="siteService">The site service that holds the store's templates.</param>
    /// <param name="logger">The logger.</param>
    public LiquidCommerceEmailTemplates(IServiceProvider serviceProvider, ISiteService siteService, ILogger<LiquidCommerceEmailTemplates> logger)
    {
        _serviceProvider = serviceProvider;
        _siteService = siteService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CommerceEmailMessage> ApplyAsync(CommerceEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var built = new CommerceEmailMessage(context.Subject, context.Text, context.Html);

        if (string.IsNullOrEmpty(context.Kind) || _serviceProvider.GetService<ILiquidTemplateManager>() is not { } liquid)
        {
            return built;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<OrderEmailTemplateSettings>();
        var template = settings.Templates is not null && settings.Templates.TryGetValue(context.Kind, out var found) && found.IsEnabled ? found : null;

        if (template is null && string.IsNullOrWhiteSpace(settings.Layout))
        {
            return built;
        }

        try
        {
            var values = new Dictionary<string, FluidValue>(StringComparer.Ordinal)
            {
                ["Content"] = StringValue.Create(context.Html ?? string.Empty, encode: false),
                ["Subject"] = StringValue.Create(context.Subject ?? string.Empty),
                ["StoreName"] = StringValue.Create(site.SiteName ?? string.Empty),
            };

            foreach (var (name, model) in context.Models ?? new Dictionary<string, object>())
            {
                values[name] = model is null ? NilValue.Instance : FluidValue.Create(ToPlain(ToJson(model)), _options);
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

            // A text-only email stays text-only, unless the store's template wrote a body for it.
            if (!string.IsNullOrWhiteSpace(settings.Layout) && !string.IsNullOrEmpty(html))
            {
                html = await liquid.RenderStringAsync(settings.Layout, HtmlEncoder.Default, values);
            }

            // The text part follows the HTML a template wrote; with only a layout, the built text stays.
            var text = template is not null && !string.IsNullOrWhiteSpace(template.Body) ? ToText(html) : context.Text;

            return new CommerceEmailMessage(string.IsNullOrWhiteSpace(subject) ? context.Subject : subject, text, html);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The store's template of the '{Kind}' email couldn't render; the built email was sent.", context.Kind);

            return built;
        }
    }

    /// <summary>
    /// Returns a record as JSON, with enums by name, as the template reads it.
    /// </summary>
    /// <param name="model">The record.</param>
    internal static JsonNode ToJson(object model)
        => JsonSerializer.SerializeToNode(model, model.GetType(), _json);

    /// <summary>
    /// Returns JSON as dictionaries, lists and values, which Liquid reads without registering a type: the template reads
    /// <c>{{ Order.OrderNumber }}</c>, <c>{{ Order.Lines[0].Title }}</c> and so on.
    /// </summary>
    /// <param name="node">The JSON.</param>
    internal static object ToPlain(JsonNode node)
        => node switch
        {
            JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ToPlain(pair.Value), StringComparer.OrdinalIgnoreCase),
            JsonArray array => array.Select(ToPlain).ToList(),
            JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            JsonValue value when value.TryGetValue<decimal>(out var number) => number,
            JsonValue value => value.ToString(),
            _ => null,
        };

    /// <summary>
    /// Returns the text of an HTML body, with a line for each paragraph, heading, list item and row.
    /// </summary>
    /// <param name="html">The HTML.</param>
    internal static string ToText(string html)
        => System.Net.WebUtility.HtmlDecode(Tags().Replace(Breaks().Replace(html ?? string.Empty, "\n"), string.Empty)).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</h[1-6]>|</li>|</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();
}
