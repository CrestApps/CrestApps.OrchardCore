using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// The response a formula is evaluated for, for functions that read more than their arguments (SCORE, AGE). It flows
/// with the evaluation call only.
/// </summary>
public sealed class SurveyFormulaContext
{
    private static readonly AsyncLocal<SurveyFormulaContext> _current = new();

    /// <summary>
    /// Gets the context of the evaluation running, or <see langword="null"/>.
    /// </summary>
    public static SurveyFormulaContext Current => _current.Value;

    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets today in the tenant time zone.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets the score of a question by its name, or <see langword="null"/>.
    /// </summary>
    /// <param name="questionName">The question name.</param>
    /// <returns>The score, or <see langword="null"/>.</returns>
    public decimal? ScoreOf(string questionName)
    {
        var question = Definition?.Pages?.SelectMany(page => page.Questions ?? []).FirstOrDefault(item => string.Equals(item.Name, questionName, StringComparison.Ordinal));

        return question is not null && Response?.Answers is not null && Response.Answers.TryGetValue(question.Id, out var answer) ? answer.Score : null;
    }

    /// <summary>
    /// Makes a context current until the returned scope is disposed.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>The scope.</returns>
    public static IDisposable Use(SurveyFormulaContext context)
    {
        var previous = _current.Value;
        _current.Value = context;

        return new Restore(previous);
    }

    private sealed class Restore : IDisposable
    {
        private readonly SurveyFormulaContext _previous;

        public Restore(SurveyFormulaContext previous)
        {
            _previous = previous;
        }

        public void Dispose()
            => _current.Value = _previous;
    }
}
