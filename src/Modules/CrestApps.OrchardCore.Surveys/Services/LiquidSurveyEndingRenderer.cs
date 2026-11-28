using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using Fluid.Values;
using Microsoft.Extensions.Logging;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Renders an ending's message: the author's message as Liquid first, with the survey, the response, the answers by
/// question name and the score, in restricted Liquid (<see cref="RestrictedLiquid"/>, as the invitation templates); then
/// piping of <c>{name}</c> references; then the HTML sanitizer.
/// </summary>
/// <remarks>
/// A respondent's text never reaches the Liquid parser. Only the message the author wrote is parsed; answers are
/// values of the model, and every value Liquid writes is HTML-encoded with <c>{</c> and <c>}</c> encoded too, so the
/// piping that follows cannot read a reference out of an answer. Piped values are HTML-encoded with their braces
/// encoded as well. Anyone who may edit a draft writes the message, so it runs with the restricted filters and the four
/// values above only, never Orchard Core's full Liquid surface (content by id, saved queries, the site settings).
/// </remarks>
public sealed partial class LiquidSurveyEndingRenderer : ISurveyEndingRenderer
{
    private static readonly HtmlEncoder _encoder = CreateEncoder();

    private readonly SurveyPipingRenderer _piping;
    private readonly IHtmlSanitizerService _sanitizer;
    private readonly ISurveyControlRegistry _registry;
    private readonly ILocalClock _localClock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidSurveyEndingRenderer"/> class.
    /// </summary>
    /// <param name="piping">The piping renderer.</param>
    /// <param name="sanitizer">The HTML sanitizer.</param>
    /// <param name="registry">The control registry.</param>
    /// <param name="localClock">The local clock, for the site's time zone in dates.</param>
    /// <param name="logger">The logger.</param>
    public LiquidSurveyEndingRenderer(
        SurveyPipingRenderer piping,
        IHtmlSanitizerService sanitizer,
        ISurveyControlRegistry registry,
        ILocalClock localClock,
        ILogger<LiquidSurveyEndingRenderer> logger)
    {
        _piping = piping;
        _sanitizer = sanitizer;
        _registry = registry;
        _localClock = localClock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(Survey survey, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending, bool showScore)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(response);

        // The author's message, as written: nothing a respondent typed is in it yet.
        var message = ending?.Message;

        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        if (RestrictedLiquid.HasLiquid(message))
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

                var options = RestrictedLiquid.Options;
                var rendered = await RestrictedLiquid.RenderAsync(message, new Dictionary<string, FluidValue>(StringComparer.Ordinal)
                {
                    ["Survey"] = FluidValue.Create(new Dictionary<string, object> { ["Name"] = survey?.Name, ["Title"] = survey?.Title }, options),
                    ["Response"] = FluidValue.Create(new Dictionary<string, object> { ["Number"] = response.Number, ["Status"] = response.Status.ToString(), ["Percent"] = showScore ? response.Percent : null, ["Passed"] = showScore ? response.Passed : null }, options),
                    ["Answers"] = FluidValue.Create(answers, options),
                    ["Score"] = showScore && response.Score is { } score ? NumberValue.Create(score) : NilValue.Instance,
                }, _encoder, await RestrictedLiquidContexts.CreateAsync(_localClock));

                if (rendered is null)
                {
                    // The message doesn't parse or uses a filter outside the restricted set (an ending published before
                    // endings were restricted; the definition validator refuses it now): it shows without its Liquid, so
                    // respondents never see template code.
                    _logger.LogWarning("The ending message of a survey doesn't parse or uses a Liquid filter that isn't available in it; it was shown without its Liquid.");
                    message = WithoutLiquid(message);
                }
                else
                {
                    message = rendered;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The ending message of a survey failed to render.");
            }
        }

        // Piping last: the values are HTML-encoded and their braces encoded, and nothing is parsed afterwards.
        message = _piping.Render(message, definition, response, showScore: showScore);

        return _sanitizer.Sanitize(message);
    }

    /// <summary>
    /// Removes the Liquid tags and outputs (<c>{{ ... }}</c> and <c>{% ... %}</c>) from a message, keeping its text.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The message without its Liquid.</returns>
    internal static string WithoutLiquid(string message)
        => string.IsNullOrEmpty(message) ? message : LiquidMarkup().Replace(message, string.Empty);

    [GeneratedRegex(@"\{\{.*?\}\}|\{%.*?%\}", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LiquidMarkup();

    private static HtmlEncoder CreateEncoder()
    {
        // HTML-encodes like the default encoder, and also encodes the braces so a written value is never read as a
        // piping reference.
        var settings = new TextEncoderSettings(UnicodeRanges.BasicLatin);
        settings.ForbidCharacters('{', '}');

        return HtmlEncoder.Create(settings);
    }
}
