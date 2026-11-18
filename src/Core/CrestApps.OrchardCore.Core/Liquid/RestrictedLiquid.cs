using System.Globalization;
using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Fluid.Filters;
using Fluid.Values;

namespace CrestApps.OrchardCore.Core.Liquid;

/// <summary>
/// Liquid for text that people other than developers write (e-mail templates, reply templates): no functions, a cap on
/// steps and recursion, only the values the caller passes, and a fixed set of filters. Templates render in the site's
/// time zone and culture (<see cref="RestrictedLiquidContext"/>), so the <c>date</c>, <c>format_date</c> and
/// <c>local</c> filters show a reader's time, never UTC, and <c>t</c> localizes a text as Orchard's filter does.
/// A template that uses any other filter is refused on save (<see cref="TryValidate"/>) and not rendered.
/// </summary>
public static class RestrictedLiquid
{
    // The most statements a template may run, so a loop written by mistake can't hold a request.
    private const int MaxSteps = 100_000;
    private const string ContextKey = "CrestApps.RestrictedLiquid.Context";

    private static readonly FluidParser _parser = new(new FluidParserOptions { AllowFunctions = false });

    private static readonly TemplateOptions _options = new TemplateOptionsBuilder()
        .WithMaxSteps(MaxSteps)
        .WithMaxRecursion(20)
        .AddFilter("date", LocalDateAsync)
        .AddFilter("format_date", LocalFormatDateAsync)
        .AddFilter("local", LocalAsync)
        .AddFilter("utc", UtcAsync)
        .AddFilter("t", LocalizeAsync)
        .Build();

    /// <summary>
    /// Gets the options templates render with.
    /// </summary>
    public static TemplateOptions Options => _options;

    /// <summary>
    /// Gets the names of the filters a template may use: Fluid's standard filters, with <c>date</c> and
    /// <c>format_date</c> in the site's time zone, and <c>local</c>, <c>utc</c> and <c>t</c>.
    /// </summary>
    public static IReadOnlyCollection<string> FilterNames { get; } = [.. _options.Filters.Select(filter => filter.Key).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Whether a text has Liquid in it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has an output or a tag.</returns>
    public static bool HasLiquid(string text)
        => !string.IsNullOrEmpty(text) && (text.Contains("{{", StringComparison.Ordinal) || text.Contains("{%", StringComparison.Ordinal));

    /// <summary>
    /// Checks a template: it parses, and it uses only the filters in <see cref="FilterNames"/>.
    /// </summary>
    /// <param name="source">The template.</param>
    /// <param name="error">Why it's refused.</param>
    /// <returns><see langword="true"/> when it may be saved.</returns>
    public static bool TryValidate(string source, out string error)
    {
        error = null;

        if (!HasLiquid(source))
        {
            return true;
        }

        if (!_parser.TryParse(source, out var template, out error))
        {
            return false;
        }

        if (UnknownFilters(template) is { Count: > 0 } unknown)
        {
            error = $"These filters aren't available here: {string.Join(", ", unknown)}.";

            return false;
        }

        return true;
    }

    /// <summary>
    /// Renders a template with only the given values.
    /// </summary>
    /// <param name="source">The template.</param>
    /// <param name="values">The values it may read.</param>
    /// <param name="encoder">The encoder of the output.</param>
    /// <param name="context">The time zone, culture and localizer to render with; UTC and the current culture when
    /// <see langword="null"/>.</param>
    /// <returns>The text, or <see langword="null"/> when the template doesn't parse or uses a filter that isn't
    /// available.</returns>
    public static async ValueTask<string> RenderAsync(string source, IEnumerable<KeyValuePair<string, FluidValue>> values, TextEncoder encoder, RestrictedLiquidContext context = null)
    {
        ArgumentNullException.ThrowIfNull(encoder);

        if (!HasLiquid(source))
        {
            return source ?? string.Empty;
        }

        if (!_parser.TryParse(source, out var template, out _) || UnknownFilters(template).Count > 0)
        {
            return null;
        }

        context ??= new RestrictedLiquidContext();

        var templateContext = new TemplateContext(_options)
        {
            TimeZone = context.TimeZone ?? TimeZoneInfo.Utc,
            CultureInfo = context.Culture ?? CultureInfo.CurrentUICulture,
        };

        templateContext.AmbientValues[ContextKey] = context;

        foreach (var (name, value) in values ?? [])
        {
            templateContext.SetValue(name, value);
        }

        return await template.RenderAsync(templateContext, encoder);
    }

    private static List<string> UnknownFilters(IFluidTemplate template)
    {
        var finder = new FilterFinder();
        finder.VisitTemplate(template);

        return [.. finder.Names.Where(name => !_options.Filters.TryGetValue(name, out _)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // A stored UTC time (a DateTime, or the ISO text a model's JSON holds) moved to the site's time zone.
    private static FluidValue ToLocal(FluidValue input, TemplateContext context)
    {
        DateTimeOffset? value = input.Type switch
        {
            FluidValues.DateTime => input.ToObjectValue(context) switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, dateTime.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : dateTime.Kind)),
                _ => null,
            },
            FluidValues.String when DateTimeOffset.TryParse(input.ToStringValue(context), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) => parsed,
            _ => null,
        };

        return value is { } found
            ? FluidValue.Create(TimeZoneInfo.ConvertTime(found, context.TimeZone ?? TimeZoneInfo.Utc), context.Options)
            : input;
    }

    private static ValueTask<FluidValue> LocalDateAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
        => MiscFilters.Date(ToLocal(input, context), arguments, context);

    private static ValueTask<FluidValue> LocalFormatDateAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
        => MiscFilters.FormatDate(ToLocal(input, context), arguments, context);

    private static ValueTask<FluidValue> LocalAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
        => new(ToLocal(input, context));

    private static ValueTask<FluidValue> UtcAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var local = ToLocal(input, context);

        return new(local.ToObjectValue(context) is DateTimeOffset offset ? FluidValue.Create(offset.ToUniversalTime(), context.Options) : input);
    }

    // Like Orchard's t filter: the text is looked up, and the arguments fill its {0}, {1}... placeholders.
    private static ValueTask<FluidValue> LocalizeAsync(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var text = input.ToStringValue(context);
        var parameters = arguments.Values.Select(value => value.ToObjectValue(context)).ToArray();
        var localize = context.AmbientValues.TryGetValue(ContextKey, out var value) && value is RestrictedLiquidContext { Localize: { } found }
            ? found
            : null;

        var result = localize is not null
            ? localize(text, parameters)
            : parameters.Length == 0 ? text : string.Format(context.CultureInfo, text, parameters);

        return new(StringValue.Create(result));
    }

    private sealed class FilterFinder : AstVisitor
    {
        public List<string> Names { get; } = [];

        protected override Expression VisitFilterExpression(FilterExpression filterExpression)
        {
            Names.Add(filterExpression.Name);

            return base.VisitFilterExpression(filterExpression);
        }
    }
}

/// <summary>
/// What a restricted template renders with: the reader's time zone and culture, and how <c>t</c> localizes a text.
/// </summary>
public sealed class RestrictedLiquidContext
{
    /// <summary>
    /// Gets the time zone dates are shown in. Callers pass the site's or the reader's, from Orchard's
    /// <c>ILocalClock</c>; UTC only when none is known.
    /// </summary>
    public TimeZoneInfo TimeZone { get; init; }

    /// <summary>
    /// Gets the culture numbers and dates are written in.
    /// </summary>
    public CultureInfo Culture { get; init; }

    /// <summary>
    /// Gets what <c>t</c> does with a text and its arguments.
    /// </summary>
    public Func<string, object[], string> Localize { get; init; }
}
