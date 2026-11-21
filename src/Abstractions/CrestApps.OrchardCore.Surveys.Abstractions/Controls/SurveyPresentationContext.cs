using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The state of building one response's plan.
/// </summary>
public sealed class SurveyPresentationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyPresentationContext"/> class.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="plan">The plan being built.</param>
    /// <param name="random">The seeded random generator of the response.</param>
    public SurveyPresentationContext(SurveyDefinition definition, SurveyResponsePlan plan, SurveyRandom random)
    {
        Definition = definition;
        Plan = plan;
        Random = random;
    }

    /// <summary>
    /// Gets the definition.
    /// </summary>
    public SurveyDefinition Definition { get; }

    /// <summary>
    /// Gets the plan being built; a handler sets <see cref="SurveyResponsePlan.ChoiceOrder"/> for its question.
    /// </summary>
    public SurveyResponsePlan Plan { get; }

    /// <summary>
    /// Gets the seeded random generator of the response.
    /// </summary>
    public SurveyRandom Random { get; }
}
