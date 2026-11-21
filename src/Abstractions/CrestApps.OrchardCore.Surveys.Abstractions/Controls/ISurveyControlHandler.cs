using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Everything a question type does with its questions and answers: defaults, publish checks, presentation, reading and
/// checking answers, logic values, text, export columns and index rows. Register an implementation with
/// <c>services.AddSurveyControl&lt;THandler&gt;(name, ...)</c>. Derive from <c>SurveyControlHandlerBase&lt;TSettings&gt;</c>
/// in the core library to override only what differs.
/// </summary>
public interface ISurveyControlHandler
{
    /// <summary>
    /// Gets the name of the question type the handler serves.
    /// </summary>
    string ControlType { get; }

    /// <summary>
    /// Sets the defaults of a new question.
    /// </summary>
    /// <param name="question">The new question.</param>
    void Initialize(SurveyQuestion question);

    /// <summary>
    /// Checks a question when the survey is published, adding errors and warnings to the context.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The validation context.</param>
    /// <returns>A task.</returns>
    Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context);

    /// <summary>
    /// Prepares how the question shows for one response, such as the order of its choices, using the response's seeded
    /// random generator.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="context">The presentation context.</param>
    void PreparePresentation(SurveyQuestion question, SurveyPresentationContext context);

    /// <summary>
    /// Gets the settings respondents may see; the key never leaves the server.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The settings without key values.</returns>
    JsonObject GetRespondentSettings(SurveyQuestion question);

    /// <summary>
    /// Reads the answer from a posted form (inputs named under <c>q.{questionId}</c>) or from a JSON body.
    /// </summary>
    /// <param name="context">The read context; the handler sets its answer.</param>
    /// <returns>A task.</returns>
    Task ReadAnswerAsync(SurveyAnswerReadContext context);

    /// <summary>
    /// Checks an answer: required, ranges and validation rules. Errors are added to the context.
    /// </summary>
    /// <param name="context">The validation context.</param>
    /// <returns>A task.</returns>
    Task ValidateAnswerAsync(SurveyAnswerValidationContext context);

    /// <summary>
    /// Gets whether an answer holds nothing.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it is empty.</returns>
    bool IsEmpty(SurveyQuestion question, SurveyAnswer answer);

    /// <summary>
    /// Gets the values conditions compare for an answer (or one row of it).
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <param name="rowId">The row id, or <see langword="null"/> for the whole answer.</param>
    /// <returns>The condition value.</returns>
    ConditionValue GetConditionValue(SurveyQuestion question, SurveyAnswer answer, string rowId);

    /// <summary>
    /// Gets the value formulas read for an answer: a <see cref="long"/>, <see cref="decimal"/>, <see cref="string"/>,
    /// <see cref="bool"/>, <see cref="DateTime"/> or <see langword="null"/>.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <param name="rowId">The row id, or <see langword="null"/>.</param>
    /// <returns>The value.</returns>
    object GetFormulaValue(SurveyQuestion question, SurveyAnswer answer, string rowId);

    /// <summary>
    /// Gets the type of the value formulas read.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="rowId">The row id, or <see langword="null"/>.</param>
    /// <returns>The type.</returns>
    DataFieldType GetFormulaType(SurveyQuestion question, string rowId);

    /// <summary>
    /// Formats an answer as plain text, for piping, exports and notices.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <param name="context">The format context.</param>
    /// <returns>The text, or an empty string.</returns>
    string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context);

    /// <summary>
    /// Describes the export columns of a question. Keys are stable: <c>{name}</c>, <c>{name}.{row}</c>,
    /// <c>{name}.other</c>.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The columns.</returns>
    IEnumerable<SurveyExportColumn> DescribeColumns(SurveyQuestion question);

    /// <summary>
    /// Writes the export columns of an answer into a row.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer, or <see langword="null"/>.</param>
    /// <param name="row">The row, by column key.</param>
    /// <param name="context">The format context.</param>
    void WriteColumns(SurveyQuestion question, SurveyAnswer answer, IDictionary<string, object> row, SurveyFormatContext context);

    /// <summary>
    /// Gets the answer index rows of a completed answer, which results aggregate in SQL.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The rows.</returns>
    IEnumerable<SurveyAnswerIndexRow> GetIndexRows(SurveyQuestion question, SurveyAnswer answer);
}
