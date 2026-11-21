using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;

/// <summary>
/// Records the value of a hidden variable (from the link, the collector or the consumer) as an answer, so it shows in
/// results and exports. Respondents never see it and nothing is read from the form.
/// </summary>
public sealed class HiddenControlHandler : SurveyControlHandlerBase<HiddenSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HiddenControlHandler"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public HiddenControlHandler(IStringLocalizer<HiddenControlHandler> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string ControlType => SurveyControlNames.Hidden;

    /// <inheritdoc />
    public override Task ReadAnswerAsync(SurveyAnswerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var settings = context.Question.GetSettings<HiddenSettings>();
        var name = string.IsNullOrWhiteSpace(settings.VariableName) ? context.Question.Name : settings.VariableName.Trim();

        context.Answer = context.Response?.Variables is not null
            && context.Response.Variables.TryGetValue(name, out var value)
            && !string.IsNullOrEmpty(value)
                ? new SurveyAnswer { Values = [Cut(value, 4000)] }
                : null;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidateAnswerAsync(SurveyAnswerValidationContext context)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override async Task ValidateDefinitionAsync(SurveyQuestion question, SurveyDefinitionValidationContext context)
    {
        await base.ValidateDefinitionAsync(question, context);

        var settings = question.GetSettings<HiddenSettings>();
        var name = string.IsNullOrWhiteSpace(settings.VariableName) ? question.Name : settings.VariableName.Trim();

        if (!context.Definition.Variables.Any(variable => string.Equals(variable?.Name, name, StringComparison.Ordinal)))
        {
            context.AddWarning("hidden-variable", S["Question {0} records the variable {1}, which the survey does not declare.", question.Name, name], question.Id);
        }
    }
}
