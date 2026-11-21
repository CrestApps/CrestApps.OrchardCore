using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.ShortText;

/// <summary>
/// A short text on one line.
/// </summary>
public sealed class ShortTextControlHandler : SurveyControlHandlerBase<ShortTextSettings>
{
    /// <summary>
    /// The most characters when the settings give none.
    /// </summary>
    public const int DefaultMaxLength = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortTextControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public ShortTextControlHandler(IStringLocalizer<ShortTextControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.ShortText;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = ReadValue(context, context.Prefix);

        context.Answer = value is null ? null : new SurveyAnswer { Values = [Cut(value, 4000)] };

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

        var settings = context.Question.GetSettings<ShortTextSettings>();
        var max = settings.MaxLength is > 0 ? settings.MaxLength.Value : DefaultMaxLength;

        if (value.Length > max)
        {
            context.Errors.Add(S["The answer must have at most {0} characters.", max]);
        }

        if (settings.InputType == ShortTextInputType.Email && !IsEmail(value))
        {
            context.Errors.Add(S["Enter a valid e-mail address."]);
        }

        if (settings.InputType == ShortTextInputType.Url
            && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            context.Errors.Add(S["Enter a valid web address."]);
        }
    }
}
