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
/// Exports the employers.
/// </summary>
public sealed class EmployersDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<EmployersDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<EmployersDeploymentStep>("Employers");

    /// <inheritdoc />
    public override string Name => PayrollConstants.EmployersRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class EmployersDeploymentSource : CatalogDeploymentSource<EmployersDeploymentStep, Employer>
{
    public EmployersDeploymentSource(IEmployerManager manager)
        : base(EmployersRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<Employer> Order(IEnumerable<Employer> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class EmployersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(EmployersDeploymentStep);

    protected override string DisplayText => "Employers";

    protected override string Description => "Exports every employer with its registrations. No person's data is in it.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
