using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.LongText;

/// <summary>
/// A long text or essay on several lines.
/// </summary>
public sealed class LongTextControlHandler : SurveyControlHandlerBase<LongTextSettings>
{
    private const int MaxLength = 20000;

    /// <summary>
    /// Initializes a new instance of the <see cref="LongTextControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public LongTextControlHandler(IStringLocalizer<LongTextControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.LongText;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = ReadValue(context, context.Prefix);

        context.Answer = value is null ? null : new SurveyAnswer { Values = [Cut(value, MaxLength)] };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
    {
        await base.ValidateAnswerAsync(context);

        var value = context.Answer?.Values?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var settings = context.Question.GetSettings<LongTextSettings>();
        var words = CountWords(value);

        if (settings.MinWords is > 0 && words < settings.MinWords)
        {
            context.Errors.Add(S["Write at least {0} words.", settings.MinWords]);
        }

        if (settings.MaxWords is > 0 && words > settings.MaxWords)
        {
            context.Errors.Add(S["Write at most {0} words.", settings.MaxWords]);
        }
    }

    /// <summary>
    /// Counts the words of a text.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The count.</returns>
    public static int CountWords(string value)
        => string.IsNullOrWhiteSpace(value) ? 0 : value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
}
