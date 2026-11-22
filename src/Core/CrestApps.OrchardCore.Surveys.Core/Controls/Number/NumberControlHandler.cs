using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Number;

/// <summary>
/// A number.
/// </summary>
public sealed class NumberControlHandler : SurveyControlHandlerBase<NumberSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NumberControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public NumberControlHandler(IStringLocalizer<NumberControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Number;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var settings = question.GetSettings<NumberSettings>();

        if (settings.Min is { } min && settings.Max is { } max && min > max)
        {
            context.AddError("min-max", S["The smallest value of question {0} is more than its largest value.", question.Name], question.Id);
        }
    }

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = ReadValue(context, context.Prefix);

        if (text is null)
        {
            context.Answer = null;
        }
        else if (TryParseNumber(text, out var number))
        {
            context.Answer = new SurveyAnswer { Number = number };
        }
        else
        {
            context.Answer = null;
            context.Errors.Add(S["Enter a number."]);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        await base.ValidateAnswerAsync(context);

        if (context.Answer?.Number is not { } number)
        {
            return;
        }

        ValidateRange(context, number, context.Question.GetSettings<NumberSettings>());
    }

    /// <inheritdoc />
    public override DataFieldType GetFormulaType(SurveyQuestion question, string rowId)
        => DataFieldType.Decimal;

    /// <inheritdoc />
    public override string FormatAnswer(SurveyQuestion question, SurveyAnswer answer, SurveyFormatContext context)
    {
        var text = base.FormatAnswer(question, answer, context);
        var unit = question.GetSettings<NumberSettings>().Unit;

        return answer?.Number is not null && context?.UseValues != true && !string.IsNullOrWhiteSpace(unit) ? $"{text} {unit}" : text;
    }

    private void ValidateRange(SurveyAnswerValidationContext context, decimal number, NumberSettings settings)
    {
        if (settings.Min is { } min && number < min)
        {
            context.Errors.Add(S["Enter a number of at least {0}.", min.ToString(CultureInfo.CurrentCulture)]);
        }

        if (settings.Max is { } max && number > max)
        {
            context.Errors.Add(S["Enter a number of at most {0}.", max.ToString(CultureInfo.CurrentCulture)]);
        }

        // A decimal has at most 28 decimals: rounding to more would throw.
        if (settings.Decimals is { } decimals && decimals >= 0 && Math.Round(number, Math.Min(decimals, 28)) != number)
        {
            context.Errors.Add(S["Enter a number with at most {0} decimals.", decimals]);
        }

        if (settings.Step is > 0 && !IsOnStep(number, settings.Min ?? 0, settings.Step.Value))
        {
            context.Errors.Add(S["Enter a number in steps of {0}.", settings.Step.Value.ToString(CultureInfo.CurrentCulture)]);
        }
    }

    // A number so far from the start of the steps that the difference doesn't fit a decimal is off the steps: the
    // answer is invalid instead of failing the post.
    private static bool IsOnStep(decimal number, decimal start, decimal step)
    {
        try
        {
            return (number - start) % step == 0;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
