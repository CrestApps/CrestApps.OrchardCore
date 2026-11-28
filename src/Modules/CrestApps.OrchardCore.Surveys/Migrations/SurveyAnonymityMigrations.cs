using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Asks, once, for the <see cref="SurveyAnonymityUpgradeStep"/>, which protects the anonymity secrets of sites made
/// before they were hardened and clears the exact participation times of anonymous surveys. The step runs in the
/// background, so the walk never holds up the tenant's activation; on a new site it finds nothing to upgrade.
/// </summary>
internal sealed class SurveyAnonymityMigrations : DataMigration
{
    private readonly ResumableSteps _steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymityMigrations"/> class.
    /// </summary>
    /// <param name="steps">The resumable upgrade steps.</param>
    public SurveyAnonymityMigrations(ResumableSteps steps)
    {
        _steps = steps;
    }

    /// <summary>
    /// Asks for the anonymity upgrade step.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await _steps.RequestAsync(SurveyAnonymityUpgradeStep.StepName);

        return 1;
    }
}
