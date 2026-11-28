using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Checks the quotas of a definition before it is published: a limit, unique codes, rule sources that exist, and an
/// ending that screens out for quotas that screen out.
/// </summary>
public sealed class SurveyQuotaValidationContributor : ISurveyDefinitionValidationContributor
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuotaValidationContributor"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyQuotaValidationContributor(IStringLocalizer<SurveyQuotaValidationContributor> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task ValidateAsync(SurveyDefinitionValidationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var definition = context.Definition;
        var quotas = definition?.Quotas ?? [];

        if (quotas.Count == 0)
        {
            return Task.CompletedTask;
        }

        var questions = (definition.Pages ?? []).SelectMany(page => page.Questions ?? []).Where(question => !string.IsNullOrEmpty(question?.Id)).Select(question => question.Id).ToHashSet(StringComparer.Ordinal);
        var variables = (definition.Variables ?? []).Where(variable => !string.IsNullOrEmpty(variable?.Name)).Select(variable => variable.Name).ToHashSet(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var quota in quotas.Where(quota => quota is not null))
        {
            var label = string.IsNullOrWhiteSpace(quota.Title) ? quota.Name : quota.Title;

            if (quota.Limit <= 0)
            {
                context.AddError("quota-limit", S["The quota {0} needs a limit above zero.", label]);
            }

            if (!string.IsNullOrEmpty(quota.Name) && !names.Add(quota.Name))
            {
                context.AddError("quota-duplicate-name", S["Two quotas use the code {0}.", quota.Name]);
            }

            foreach (var source in ConditionalVisibility.Sources(quota.Rule))
            {
                if (!IsKnownSource(source, questions, variables))
                {
                    context.AddError("quota-rule-source", S["The rule of the quota {0} reads something the survey does not have.", label]);

                    break;
                }
            }

            if (quota.Action == QuotaAction.ScreenOut)
            {
                var ending = (definition.Endings ?? []).FirstOrDefault(candidate => candidate.Id == quota.EndingId);

                if (ending is null)
                {
                    context.AddError("quota-ending", S["The quota {0} screens out respondents, so it needs an ending.", label]);
                }
                else if (ending.Outcome != EndingOutcome.ScreenedOut)
                {
                    context.AddWarning("quota-ending-outcome", S["The ending of the quota {0} counts as completed; respondents of a full quota are still screened out.", label]);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsKnownSource(string source, HashSet<string> questions, HashSet<string> variables)
    {
        if (source.StartsWith("var:", StringComparison.Ordinal))
        {
            return variables.Contains(source.Substring(4));
        }

        if (source.Contains(':', StringComparison.Ordinal) || source is "score" or "collector" or "culture")
        {
            // Scores, context values, the collector and the culture are known only at run time.
            return true;
        }

        var dot = source.IndexOf('.', StringComparison.Ordinal);

        return questions.Contains(dot < 0 ? source : source.Substring(0, dot));
    }
}
