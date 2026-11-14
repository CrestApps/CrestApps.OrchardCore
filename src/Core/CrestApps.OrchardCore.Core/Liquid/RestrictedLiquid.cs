using System.Text.Encodings.Web;
using Fluid;
using Fluid.Values;

namespace CrestApps.OrchardCore.Core.Liquid;

/// <summary>
/// Liquid for text that site administrators write and that renders inside other people's requests, such as reply
/// templates and e-mail templates. It sees only the values the caller hands it (the documented model of the template),
/// never Orchard's request, user or site objects, and a template that doesn't parse is reported instead of rendering the
/// parser's message. It needs no Liquid feature.
/// </summary>
public static class RestrictedLiquid
{
    // The most statements a template may run, so a loop written by mistake can't hold a request.
    private const int MaxSteps = 100_000;

    private static readonly FluidParser _parser = new(new FluidParserOptions { AllowFunctions = false });

    private static readonly TemplateOptions _options = new TemplateOptionsBuilder()
        .WithMaxSteps(MaxSteps)
        .WithMaxRecursion(20)
        .Build();

    /// <summary>
    /// Gets the options the values of a template are made with, so they follow the same rules as the template.
    /// </summary>
    public static TemplateOptions Options => _options;

    /// <summary>
    /// Returns whether a text has Liquid in it: an output (<c>{{ }}</c>) or a tag (<c>{% %}</c>).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool HasLiquid(string text)
        => !string.IsNullOrEmpty(text) && (text.Contains("{{", StringComparison.Ordinal) || text.Contains("{%", StringComparison.Ordinal));

    /// <summary>
    /// Checks that a template parses.
    /// </summary>
    /// <param name="source">The template.</param>
    /// <param name="error">Why it doesn't parse, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it parses.</returns>
    public static bool TryValidate(string source, out string error)
    {
        error = null;

        if (!HasLiquid(source))
        {
            return true;
        }

        return _parser.TryParse(source, out _, out error);
    }

    /// <summary>
    /// Renders a template with the given values only.
    /// </summary>
    /// <param name="source">The template.</param>
    /// <param name="values">The values it may read, by name.</param>
    /// <param name="encoder">How output is encoded: <see cref="HtmlEncoder.Default"/> for HTML, <see cref="NullEncoder.Default"/> for text.</param>
    /// <returns>The rendered text, or <see langword="null"/> when the template doesn't parse.</returns>
    public static async ValueTask<string> RenderAsync(string source, IEnumerable<KeyValuePair<string, FluidValue>> values, TextEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(encoder);

        if (!HasLiquid(source))
        {
            return source ?? string.Empty;
        }

        if (!_parser.TryParse(source, out var template, out _))
        {
            return null;
        }

        var context = new TemplateContext(_options);

        foreach (var (name, value) in values ?? [])
        {
            context.SetValue(name, value);
        }

        return await template.RenderAsync(context, encoder);
    }
}
