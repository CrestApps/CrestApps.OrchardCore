using CrestApps.Core.Templates.Tags;
using Fluid;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Templates.Rendering;

/// <summary>
/// Processes Liquid templates using the Fluid template engine.
/// </summary>
public sealed class FluidTemplateEngine : ITemplateEngine
{
    private static readonly FluidParser _parser = CreateParser();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FluidTemplateEngine> _logger;

    public FluidTemplateEngine(
        IServiceProvider serviceProvider,
        ILogger<FluidTemplateEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<string> RenderAsync(string template, IDictionary<string, object> arguments = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        if (!_parser.TryParse(template, out var fluidTemplate, out var error))
        {
            _logger.LogWarning("Failed to parse Liquid template: {Error}", error);

            return template;
        }

        var options = new Fluid.TemplateOptions
        {
            MemberAccessStrategy = new UnsafeMemberAccessStrategy()
        };

        var context = new TemplateContext(options);
        context.AmbientValues["ServiceProvider"] = _serviceProvider;
        context.AmbientValues[RenderTemplateTag.AmbientFluidParserKey] = _parser;

        if (arguments != null)
        {
            foreach (var (key, value) in arguments)
            {
                context.SetValue(key, value);
            }
        }

        var result = await fluidTemplate.RenderAsync(context);

        return NormalizeWhitespace(result);
    }

    public bool TryValidate(string template, out IList<string> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(template))
        {
            return true;
        }

        if (!_parser.TryParse(template, out _, out var error))
        {
            errors.Add(error);

            return false;
        }

        return true;
    }
    /// <summary>
    /// Normalizes whitespace in the rendered output so templates can be written
    /// with readable formatting while producing clean output for AI consumption.
    /// Collapses runs of blank lines into a single blank line and trims each line.
    /// </summary>
    internal static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lines = text.Split('\n');
        var builder = new List<string>(lines.Length);
        var previousWasBlank = true;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.TrimEnd('\r').Trim();

            if (trimmed.Length == 0)
            {
                if (!previousWasBlank)
                {
                    builder.Add(string.Empty);
                    previousWasBlank = true;
                }

                continue;
            }

            builder.Add(trimmed);
            previousWasBlank = false;
        }

        // Remove trailing blank lines.
        while (builder.Count > 0 && builder[^1].Length == 0)
        {
            builder.RemoveAt(builder.Count - 1);
        }

        return string.Join('\n', builder);
    }

    private static FluidParser CreateParser()
    {
        var parser = new FluidParser();

        parser.RegisterExpressionTag(
            RenderTemplateTag.TagName,
            RenderTemplateTag.WriteToAsync);

        return parser;
    }
}
