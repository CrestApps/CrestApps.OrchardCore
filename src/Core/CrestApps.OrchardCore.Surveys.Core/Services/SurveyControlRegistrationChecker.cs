using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Logs, when the tenant starts, the question type registrations that can't work: a registered type whose handler
/// reports another <see cref="ISurveyControlHandler.ControlType"/> (the type would silently disappear from the picker,
/// the runner and the results), a handler no type is registered for, an analyzer of a type without
/// <see cref="SurveyControlCapabilities.Aggregatable"/>, and a type with <see cref="SurveyControlCapabilities.ClientScript"/>
/// that names no script resource.
/// </summary>
public sealed class SurveyControlRegistrationChecker : ModularTenantEvents
{
    private readonly SurveyControlOptions _options;
    private readonly IEnumerable<ISurveyControlHandler> _handlers;
    private readonly IEnumerable<ISurveyControlAnalyzer> _analyzers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyControlRegistrationChecker"/> class.
    /// </summary>
    /// <param name="options">The registered types.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="analyzers">The analyzers.</param>
    /// <param name="logger">The logger.</param>
    public SurveyControlRegistrationChecker(
        IOptions<SurveyControlOptions> options,
        IEnumerable<ISurveyControlHandler> handlers,
        IEnumerable<ISurveyControlAnalyzer> analyzers,
        ILogger<SurveyControlRegistrationChecker> logger)
    {
        _options = options.Value;
        _handlers = handlers;
        _analyzers = analyzers;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task ActivatedAsync()
    {
        foreach (var problem in Check(_options, _handlers, _analyzers))
        {
            _logger.LogWarning("Survey question type registration: {Problem}", problem);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Lists the problems of the question type registrations.
    /// </summary>
    /// <param name="options">The registered types.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="analyzers">The analyzers.</param>
    /// <returns>One sentence per problem.</returns>
    public static IReadOnlyList<string> Check(SurveyControlOptions options, IEnumerable<ISurveyControlHandler> handlers, IEnumerable<ISurveyControlAnalyzer> analyzers)
    {
        ArgumentNullException.ThrowIfNull(options);

        var problems = new List<string>();
        var handlerList = (handlers ?? []).ToList();
        var handled = handlerList
            .Select(handler => handler.ControlType)
            .Where(type => !string.IsNullOrEmpty(type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in options.Controls.Values)
        {
            if (!handled.Contains(descriptor.Name))
            {
                var handler = handlerList.FirstOrDefault(item => item.GetType() == descriptor.HandlerType);

                problems.Add(handler is null
                    ? $"the type '{descriptor.Name}' has no handler, so it is not available."
                    : $"the type '{descriptor.Name}' is registered with the handler {handler.GetType().FullName}, whose ControlType is '{handler.ControlType}'. The names must match, or the type is not available.");
            }

            if (descriptor.Has(SurveyControlCapabilities.ClientScript) && string.IsNullOrEmpty(descriptor.ScriptResourceName))
            {
                problems.Add($"the type '{descriptor.Name}' declares ClientScript but sets no ScriptResourceName, so the runner loads no script for it.");
            }
        }

        foreach (var handler in handlerList.Where(handler => !string.IsNullOrEmpty(handler.ControlType) && !options.Controls.ContainsKey(handler.ControlType)))
        {
            problems.Add($"the handler {handler.GetType().FullName} reports the ControlType '{handler.ControlType}', which no AddSurveyControl call registers.");
        }

        foreach (var analyzer in analyzers ?? [])
        {
            if (!string.IsNullOrEmpty(analyzer.ControlType)
                && options.Controls.TryGetValue(analyzer.ControlType, out var descriptor)
                && !descriptor.Has(SurveyControlCapabilities.Aggregatable))
            {
                problems.Add($"the analyzer {analyzer.GetType().FullName} summarizes the type '{analyzer.ControlType}', which does not declare Aggregatable, so its results are not shown.");
            }
        }

        return problems;
    }
}
