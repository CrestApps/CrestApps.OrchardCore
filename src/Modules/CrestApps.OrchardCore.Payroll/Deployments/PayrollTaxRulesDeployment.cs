using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Deployments;

/// <summary>
/// Exports every payroll tax rule.
/// </summary>
public sealed class PayrollTaxRulesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayrollTaxRulesDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayrollTaxRulesDeploymentStep>("Payroll tax rules");

    /// <inheritdoc />
    public override string Name => PayrollConstants.TaxRulesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class PayrollTaxRulesDeploymentSource : CatalogDeploymentSource<PayrollTaxRulesDeploymentStep, PayrollTaxRule>
{
    public PayrollTaxRulesDeploymentSource(IPayrollTaxRuleManager manager)
        : base(PayrollTaxRulesRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<PayrollTaxRule> Order(IEnumerable<PayrollTaxRule> entries)
        => entries.OrderBy(entry => entry.Jurisdiction, StringComparer.Ordinal).ThenBy(entry => entry.TaxCode, StringComparer.Ordinal).ThenBy(entry => entry.EffectiveFrom);
}

internal sealed class PayrollTaxRulesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayrollTaxRulesDeploymentStep);

    protected override string DisplayText => "Payroll tax rules";

    protected override string Description => "Exports every payroll tax rule, with its dated versions.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
