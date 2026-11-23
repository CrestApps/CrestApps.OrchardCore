using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Asks, once, for the <see cref="SurveyContentSanitizingStep"/>, which cleans at rest the author HTML stored before the
/// stores sanitized on save. The step runs in the background, so the walk never holds up the tenant's activation.
/// </summary>
internal sealed class SurveyContentSanitizingMigrations : DataMigration
{
    private readonly ResumableSteps _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyContentSanitizingMigrations"/> class.
    /// </summary>
    /// <param name="steps">The resumable upgrade steps.</param>
    public SurveyContentSanitizingMigrations(ResumableSteps steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Asks for the sanitizing step.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _steps.RequestAsync(SurveyContentSanitizingStep.StepName);

        return 1;
    }
}
