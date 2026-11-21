using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;

/// <summary>
/// A value calculated by a formula. The runtime evaluates the formula (<see cref="ISurveyCalculatedControl"/>) and keeps
/// the result in the response's variables under the question name; the handler records it as the answer.
/// </summary>
public sealed class CalculatedControlHandler : SurveyControlHandlerBase<CalculatedSettings>, ISurveyCalculatedControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalculatedControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public CalculatedControlHandler(IStringLocalizer<CalculatedControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Calculated;

    /// <inheritdoc />
    public string GetFormula(SurveyQuestion question)
        => question?.GetSettings<CalculatedSettings>().Formula;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        if (string.IsNullOrWhiteSpace(GetFormula(question)))
        {
            context.AddError("formula-required", S["Question {0} needs a formula.", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Response?.Variables is null
            || !context.Response.Variables.TryGetValue(context.Question.Name, out var value)
            || string.IsNullOrEmpty(value))
        {
            context.Answer = null;
        }
        else
        {
            context.Answer = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? new SurveyAnswer { Number = number }
                : new SurveyAnswer { Values = [Cut(value, 4000)] };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Decimal;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        var format = question.GetSettings<CalculatedSettings>().Format;

        if (answer?.Number is { } number && context?.UseValues != true && !string.IsNullOrWhiteSpace(format))
        {
            try
            {
                return number.ToString(format, context?.Culture ?? CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                // An author's format that .NET cannot read falls back to the plain value below.
            }
        }

        return base.FormatAnswer(question, answer, context);
    }
}
