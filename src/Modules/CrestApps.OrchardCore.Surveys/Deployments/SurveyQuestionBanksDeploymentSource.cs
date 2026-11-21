using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Writes the SurveyQuestionBanks recipe step.
/// </summary>
internal sealed class SurveyQuestionBanksDeploymentSource : DeploymentSourceBase<SurveyQuestionBanksDeploymentStep>
{
    private readonly QuestionBankStore _bankStore;
    private readonly SurveysRecipeExporter _exporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuestionBanksDeploymentSource"/> class.
    /// </summary>
    /// <param name="bankStore">The bank store.</param>
    /// <param name="exporter">The exporter.</param>
    public SurveyQuestionBanksDeploymentSource(
        QuestionBankStore bankStore,
        SurveysRecipeExporter exporter)
    {
        _bankStore = bankStore;
        _exporter = exporter;
    }

    /// <inheritdoc />
    protected override async Task ProcessAsync(SurveyQuestionBanksDeploymentStep step, DeploymentPlanResult result)
    {
        var names = step.IncludeAll ? null : (step.BankNames ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var banks = new JsonArray();

        foreach (var bank in (await _bankStore.GetAllAsync()).Where(bank => names is null || names.Contains(bank.Name)).OrderBy(bank => bank.Name, StringComparer.Ordinal))
        {
            banks.Add(await _exporter.ExportBankAsync(bank));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = SurveysRecipeSteps.QuestionBanks,
            [SurveysRecipeSteps.QuestionBanksSection] = banks,
        });
    }
}
