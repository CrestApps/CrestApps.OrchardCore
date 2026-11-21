using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using Fluid;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Renders an ending's message: piping first, then Liquid with the survey, the response, the answers by question name
/// and the score, then the HTML sanitizer.
/// </summary>
public sealed class LiquidSurveyEndingRenderer : ISurveyEndingRenderer
{
    private readonly SurveyPipingRenderer _piping;
    private readonly ILiquidTemplateManager _liquid;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly ISurveyControlRegistry _registry;
    private readonly TemplateOptions _templateOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidSurveyEndingRenderer"/> class.
    /// </summary>
    /// <param name="piping">The piping renderer.</param>
    /// <param name="liquid">The Liquid template manager.</param>
    /// <param name="sanitizer">The HTML sanitizer.</param>
    /// <param name="registry">The control registry.</param>
    /// <param name="templateOptions">The Liquid template options.</param>
    /// <param name="logger">The logger.</param>
    public LiquidSurveyEndingRenderer(
        SurveyPipingRenderer piping,
        ILiquidTemplateManager liquid,
        IHtmlSanitizerService sanitizer,
        ISurveyControlRegistry registry,
        IOptions<TemplateOptions> templateOptions,
        ILogger<LiquidSurveyEndingRenderer> logger)
    {
        _piping = piping;
        _liquid = liquid;
        _sanitizer = sanitizer;
        _registry = registry;
        _templateOptions = templateOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(Survey survey, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        var message = _piping.Render(ending?.Message, definition, response);

        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        if (message.Contains("{{", StringComparison.Ordinal) || message.Contains("{%", StringComparison.Ordinal))
        {
            try
            {
                var answers = new Dictionary<string, string>(StringComparer.Ordinal);
                var format = new SurveyFormatContext { Definition = definition };

                foreach (var question in definition.Pages.SelectMany(page => page.Questions ?? []))
                {
                    if (!string.IsNullOrEmpty(question?.Name)
                        && response.Answers.TryGetValue(question.Id, out var answer)
                        && _registry.GetHandler(question.ControlType) is { } handler)
                    {
                        answers[question.Name] = handler.FormatAnswer(question, answer, format);
                    }
                }

                message = await _liquid.RenderStringAsync(message, HtmlEncoder.Default, null, new Dictionary<string, FluidValue>
                {
                    ["Survey"] = FluidValue.Create(new Dictionary<string, object> { ["Name"] = survey?.Name, ["Title"] = survey?.Title }, _templateOptions),
                    ["Response"] = FluidValue.Create(new Dictionary<string, object> { ["Number"] = response.Number, ["Status"] = response.Status.ToString(), ["Percent"] = response.Percent, ["Passed"] = response.Passed }, _templateOptions),
                    ["Answers"] = FluidValue.Create(answers, _templateOptions),
                    ["Score"] = response.Score is { } score ? NumberValue.Create(score) : NilValue.Instance,
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The ending message of a survey failed to render.");
            }
        }

        return _sanitizer.Sanitize(message);
    }
}
