using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Exports every income tax rule.
/// </summary>
public sealed class IncomeTaxRulesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<IncomeTaxRulesDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<IncomeTaxRulesDeploymentStep>("Income tax rules");

    /// <inheritdoc />
    public override string Name => TaxationConstants.IncomeTaxRulesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class IncomeTaxRulesDeploymentSource : CatalogDeploymentSource<IncomeTaxRulesDeploymentStep, IncomeTaxRule>
{
    public IncomeTaxRulesDeploymentSource(IIncomeTaxRuleManager manager)
        : base(IncomeTaxRulesRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<IncomeTaxRule> Order(IEnumerable<IncomeTaxRule> entries)
        => entries.OrderBy(entry => entry.Jurisdiction, StringComparer.Ordinal).ThenBy(entry => entry.TaxCode, StringComparer.Ordinal).ThenBy(entry => entry.EffectiveFrom);
}

internal sealed class IncomeTaxRulesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(IncomeTaxRulesDeploymentStep);

    protected override string DisplayText => "Income tax rules";

    protected override string Description => "Exports every income tax rule, with its dated versions.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
