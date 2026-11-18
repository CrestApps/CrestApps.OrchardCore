using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The rules a simulation test case must meet before it is saved, shared by the test case editor and the AI tools.
/// </summary>
public static class AITestCaseValidator
{
    /// <summary>
    /// The most customer turns a simulated conversation can have.
    /// </summary>
    public const int MaxTurnsLimit = 50;

    /// <summary>
    /// Checks a test case with the values it is about to be saved with.
    /// </summary>
    /// <param name="testCase">The test case.</param>
    /// <param name="S">The localizer of the caller, which words the errors.</param>
    /// <returns>An error per broken rule, named after the property it is about; empty when the test case may be saved.</returns>
    public static IReadOnlyList<ValidationResult> Validate(AITestCase testCase, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(S);

        var errors = new List<ValidationResult>();

        if (string.IsNullOrWhiteSpace(testCase.Name))
        {
            errors.Add(new ValidationResult(S["The name is required."], [nameof(AITestCase.Name)]));
        }

        if (string.IsNullOrWhiteSpace(testCase.ProfileId))
        {
            errors.Add(new ValidationResult(S["Choose the AI profile to test."], [nameof(AITestCase.ProfileId)]));
        }

        if (string.IsNullOrWhiteSpace(testCase.Scenario))
        {
            errors.Add(new ValidationResult(S["Describe the customer the AI talks to."], [nameof(AITestCase.Scenario)]));
        }

        if (testCase.ProfileVersion is <= 0)
        {
            errors.Add(new ValidationResult(S["The version must be a positive number, or empty for the current one."], [nameof(AITestCase.ProfileVersion)]));
        }

        if (testCase.MaxTurns is < 1 or > MaxTurnsLimit)
        {
            errors.Add(new ValidationResult(S["The conversation must have between 1 and 50 customer turns."], [nameof(AITestCase.MaxTurns)]));
        }

        if (testCase.PassingScore is < 0 or > 100)
        {
            errors.Add(new ValidationResult(S["The passing score must be between 0 and 100."], [nameof(AITestCase.PassingScore)]));
        }

        return errors;
    }
}
