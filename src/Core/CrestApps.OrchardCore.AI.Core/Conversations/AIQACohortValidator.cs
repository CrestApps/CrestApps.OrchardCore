using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The rules a QA cohort must meet before it is saved, shared by the QA cohort editor and the AI tools.
/// </summary>
public static class AIQACohortValidator
{
    /// <summary>
    /// Checks a cohort with the values it is about to be saved with.
    /// </summary>
    /// <param name="cohort">The cohort.</param>
    /// <param name="S">The localizer of the caller, which words the errors.</param>
    /// <returns>An error per broken rule, named after the property it is about; empty when the cohort may be saved.</returns>
    public static IReadOnlyList<ValidationResult> Validate(AIQACohort cohort, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(cohort);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(cohort.Name))
        {
            errors.Add(new ValidationResult(S["The name is required."], [nameof(AIQACohort.Name)]));
        }

        if (cohort.ProfileIds is not { Count: > 0 } || cohort.ProfileIds.All(string.IsNullOrWhiteSpace))
        {
            errors.Add(new ValidationResult(S["Choose at least one AI profile."], [nameof(AIQACohort.ProfileIds)]));
        }

        if (cohort.SamplePercent is < 1 or > 100)
        {
            errors.Add(new ValidationResult(S["The sample must be between 1 and 100 percent."], [nameof(AIQACohort.SamplePercent)]));
        }

        if (cohort.PassingScore is < 0 or > 100)
        {
            errors.Add(new ValidationResult(S["The passing score must be between 0 and 100."], [nameof(AIQACohort.PassingScore)]));
        }

        return errors;
    }
}
