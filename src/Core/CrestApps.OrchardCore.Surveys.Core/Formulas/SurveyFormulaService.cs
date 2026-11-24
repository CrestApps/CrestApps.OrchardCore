using System.Collections.Concurrent;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// Compiles and evaluates survey formulas with Orchard Core's formula language, and computes calculated variables.
/// Compiled formulas are cached per version: a version never changes. A failing formula logs a warning and evaluates to
/// a missing value; it never fails the respondent.
/// </summary>
public sealed class SurveyFormulaService
{
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyExpressionFunctionCatalog _catalog;
    private readonly IMemoryCache _memoryCache;
    private readonly IClock _clock;
    private readonly IStringLocalizer S;
    private readonly ILogger _logger;
    private readonly string _tenant;
    private readonly ISiteService _siteService;

    private string _timeZoneId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyFormulaService"/> class.
    /// </summary>
    /// <param name="registry">The control registry.</param>
    /// <param name="catalog">The survey function catalog.</param>
    /// <param name="memoryCache">The memory cache.</param>
    /// <param name="shellSettings">The tenant settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="stringLocalizer">The localizer of compile errors.</param>
    /// <param name="logger">The logger.</param>
    public SurveyFormulaService(
        ISurveyControlRegistry registry,
        SurveyExpressionFunctionCatalog catalog,
        IMemoryCache memoryCache,
        ShellSettings shellSettings,
        IClock clock,
        ISiteService siteService,
        IStringLocalizer<SurveyFormulaService> stringLocalizer,
        ILogger<SurveyFormulaService> logger)
    {
        _registry = registry;
        _catalog = catalog;
        _memoryCache = memoryCache;
        _clock = clock;
        _siteService = siteService;
        S = stringLocalizer;
        _logger = logger;
        _tenant = shellSettings?.Name ?? string.Empty;
    }

    /// <summary>
    /// Reads the site's time zone, which formulas read the date and time in. The runtime calls it before it evaluates
    /// anything; until it runs, formulas use the server's time zone, as Orchard Core does for a site without one.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task PrepareAsync()
    {
        if (_timeZoneId is not null || _siteService is null)
        {
            return;
        }

        var site = await _siteService.GetSiteSettingsAsync();

        _timeZoneId = !string.IsNullOrEmpty(site?.TimeZoneId) ? site.TimeZoneId : _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;
    }

    /// <summary>
    /// Gets the time formulas read as now for a response: when it was completed, once it is finished, so a regrade or the
    /// review evaluates a rule on the date or time as the completion did; else the current time.
    /// </summary>
    /// <param name="response">The response, or <see langword="null"/>.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The time, in UTC.</returns>
    public static DateTime EvaluationUtc(SurveyResponse response, DateTime utcNow)
        => response is { Status: not SurveyResponseStatus.InProgress, CompletedUtc: { } completedUtc } ? completedUtc : utcNow;

    // A time in the site's time zone (wall clock, unspecified kind), as the formula language expects.
    private DateTime LocalTime(DateTime utc)
    {
        var zone = _timeZoneId ?? _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;

        return DateTime.SpecifyKind(CalendarTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Compiles a formula against a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="formula">The formula.</param>
    /// <param name="error">The error when it does not compile.</param>
    /// <returns><see langword="true"/> when it compiles.</returns>
    public bool TryCompile(SurveyDefinition definition, string formula, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(formula))
        {
            return true;
        }

        try
        {
            ExpressionCompiler.Compile(formula, new SurveyFormulaScope(definition, _registry), _catalog, S);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = exception.Message;

            return false;
        }
    }

    /// <summary>
    /// Gets the fields a formula reads, as written between its brackets.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="formula">The formula.</param>
    /// <returns>The fields, or none when the formula is empty or doesn't compile.</returns>
    public IReadOnlyCollection<string> GetReferences(SurveyDefinition definition, string formula)
    {
        if (string.IsNullOrWhiteSpace(formula) || definition is null)
        {
            return [];
        }

        try
        {
            var expression = ExpressionCompiler.Compile(formula, new SurveyFormulaScope(definition, _registry), _catalog, S);

            return expression.References is { } references ? [.. references] : [];
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }
    }

    /// <summary>
    /// Evaluates a formula for a response.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="formula">The formula.</param>
    /// <param name="cacheKey">The id of the version to cache the compiled formula under, or <see langword="null"/>.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or the formula fails.</returns>
    public object Evaluate(SurveyDefinition definition, SurveyResponse response, string formula, string cacheKey = null)
    {
        if (string.IsNullOrWhiteSpace(formula) || definition is null || response is null)
        {
            return null;
        }

        var compiled = Compiled(definition, cacheKey);

        try
        {
            var expression = compiled.Expressions.GetOrAdd(formula, text => ExpressionCompiler.Compile(text, compiled.Scope, _catalog, S));
            var row = BuildRow(compiled.Scope, definition, response);

            // NOW(), TODAY() and AGE() read the site's date and time, never UTC: someone born on the 23rd turns a year
            // older on the 23rd of the site's calendar, not hours earlier because UTC is already there. A finished
            // response reads the time it was completed, so a regrade or the review shows what the completion counted.
            var now = LocalTime(EvaluationUtc(response, _clock.UtcNow));

            using (SurveyFormulaContext.Use(new SurveyFormulaContext { Definition = definition, Response = response, Today = DateOnly.FromDateTime(now) }))
            {
                return expression.Evaluate(new ExpressionContext { Row = row, Now = now });
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "The survey formula {Formula} failed.", formula.SanitizeLogValue());

            return null;
        }
    }

    /// <summary>
    /// Evaluates a formula as a condition: true only for a true value or a non-zero number.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="formula">The formula.</param>
    /// <param name="cacheKey">The id of the version, or <see langword="null"/>.</param>
    /// <returns>Whether it holds.</returns>
    public bool EvaluateBoolean(SurveyDefinition definition, SurveyResponse response, string formula, string cacheKey = null)
        => Evaluate(definition, response, formula, cacheKey) switch
        {
            bool value => value,
            long number => number != 0,
            int number => number != 0,
            decimal number => number != 0,
            double number => number != 0,
            string text => bool.TryParse(text, out var parsed) && parsed,
            _ => false,
        };

    /// <summary>
    /// Computes the calculated variables of a response in dependency order and stores them in its variables. A variable
    /// caught in a cycle is left out (publishing refuses cycles).
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="cacheKey">The id of the version, or <see langword="null"/>.</param>
    public void ComputeVariables(SurveyDefinition definition, SurveyResponse response, string cacheKey = null)
    {
        if (definition is null || response is null)
        {
            return;
        }

        var calculated = (definition.Variables ?? [])
            .Where(variable => variable is not null && variable.Kind == VariableKind.Calculated && !string.IsNullOrEmpty(variable.Name) && !string.IsNullOrWhiteSpace(variable.Formula))
            .ToList();

        if (calculated.Count > 0)
        {
            foreach (var variable in OrderByDependencies(definition, calculated, cacheKey))
            {
                var value = Evaluate(definition, response, variable.Formula, cacheKey);
                response.Variables[variable.Name] = value is null ? string.Empty : DataValues.ToText(value);
            }
        }

        // Calculated questions keep their value under their name, in the invariant culture, where their handler reads it.
        foreach (var question in (definition.Pages ?? []).SelectMany(page => page?.Questions ?? []))
        {
            if (string.IsNullOrEmpty(question?.Name)
                || _registry.GetHandler(question.ControlType) is not ISurveyCalculatedControl control
                || control.GetFormula(question) is not { Length: > 0 } formula)
            {
                continue;
            }

            var value = Evaluate(definition, response, formula, cacheKey);
            response.Variables[question.Name] = value is null ? string.Empty : DataValues.ToText(value);
        }
    }

    private IEnumerable<SurveyVariable> OrderByDependencies(SurveyDefinition definition, List<SurveyVariable> calculated, string cacheKey)
    {
        var compiled = Compiled(definition, cacheKey);
        var names = calculated.ToDictionary(variable => variable.Name, StringComparer.OrdinalIgnoreCase);
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in calculated)
        {
            var reads = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var expression = compiled.Expressions.GetOrAdd(variable.Formula, text => ExpressionCompiler.Compile(text, compiled.Scope, _catalog, S));

                foreach (var reference in expression.References ?? new HashSet<string>())
                {
                    var key = reference.StartsWith("var:", StringComparison.OrdinalIgnoreCase) ? reference.Substring(4) : reference;

                    if (names.ContainsKey(key) && !string.Equals(key, variable.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        reads.Add(key);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "The formula of the survey variable {Variable} does not compile.", variable.Name.SanitizeLogValue());
            }

            dependencies[variable.Name] = reads;
        }

        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var progress = true;

        while (progress && done.Count < calculated.Count)
        {
            progress = false;

            foreach (var variable in calculated)
            {
                if (!done.Contains(variable.Name) && dependencies[variable.Name].All(done.Contains))
                {
                    done.Add(variable.Name);
                    progress = true;

                    yield return variable;
                }
            }
        }
    }

    private CompiledScope Compiled(SurveyDefinition definition, string cacheKey)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            return new CompiledScope(new SurveyFormulaScope(definition, _registry));
        }

        return _memoryCache.GetOrCreate($"Surveys:Formulas:{_tenant}:{cacheKey}", entry =>
        {
            entry.SlidingExpiration = _cacheExpiration;

            return new CompiledScope(new SurveyFormulaScope(definition, _registry));
        });
    }

    private object[] BuildRow(SurveyFormulaScope scope, SurveyDefinition definition, SurveyResponse response)
    {
        var row = new object[scope.Slots.Count];
        Dictionary<string, SurveyQuestion> questions = null;

        foreach (var field in scope.Slots)
        {
            switch (field.Kind)
            {
                case SurveyFormulaFieldKind.Question:
                    questions ??= (definition.Pages ?? []).SelectMany(page => page.Questions ?? []).Where(question => !string.IsNullOrEmpty(question?.Id)).GroupBy(question => question.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

                    if (questions.TryGetValue(field.Id, out var question) && _registry.GetHandler(question.ControlType) is { } handler)
                    {
                        response.Answers.TryGetValue(question.Id, out var answer);
                        row[field.Slot] = handler.GetFormulaValue(question, answer, field.RowId);
                    }

                    break;
                case SurveyFormulaFieldKind.Variable:
                    row[field.Slot] = response.Variables.TryGetValue(field.Id, out var text) && !string.IsNullOrEmpty(text)
                        ? DataValues.Coerce(text, field.Type)
                        : null;

                    break;
                case SurveyFormulaFieldKind.Score:
                    row[field.Slot] = response.Score;

                    break;
                case SurveyFormulaFieldKind.Percent:
                    row[field.Slot] = response.Percent;

                    break;
                case SurveyFormulaFieldKind.CategoryScore:
                    row[field.Slot] = response.CategoryScores.TryGetValue(field.Id, out var score) ? score : null;

                    break;
            }
        }

        return row;
    }

    private sealed class CompiledScope
    {
        public CompiledScope(SurveyFormulaScope scope)
        {
            Scope = scope;
        }

        public SurveyFormulaScope Scope { get; }

        public ConcurrentDictionary<string, CompiledExpression> Expressions { get; } = new(StringComparer.Ordinal);
    }
}
