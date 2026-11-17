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
/// Exports the pay schedules.
/// </summary>
public sealed class PaySchedulesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PaySchedulesDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PaySchedulesDeploymentStep>("Pay schedules");

    /// <inheritdoc />
    public override string Name => PayrollConstants.PaySchedulesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Exports the pay codes.
/// </summary>
public sealed class PayCodesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<PayCodesDeploymentStep>("Payroll");
    private static readonly LocalizationSource _title = LocalizationSource.Create<PayCodesDeploymentStep>("Pay codes");

    /// <inheritdoc />
    public override string Name => PayrollConstants.PayCodesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class PaySchedulesDeploymentSource : CatalogDeploymentSource<PaySchedulesDeploymentStep, PaySchedule>
{
    public PaySchedulesDeploymentSource(IPayScheduleManager manager)
        : base(PaySchedulesRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<PaySchedule> Order(IEnumerable<PaySchedule> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
}

internal sealed class PayCodesDeploymentSource : CatalogDeploymentSource<PayCodesDeploymentStep, PayCode>
{
    public PayCodesDeploymentSource(IPayCodeManager manager)
        : base(PayCodesRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<PayCode> Order(IEnumerable<PayCode> entries)
        => entries.OrderBy(entry => entry.Code, StringComparer.Ordinal);
}

internal sealed class PaySchedulesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PaySchedulesDeploymentStep);

    protected override string DisplayText => "Pay schedules";

    protected override string Description => "Exports every pay schedule.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class PayCodesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PayCodesDeploymentStep);

    protected override string DisplayText => "Pay codes";

    protected override string Description => "Exports every pay code, with the settings modules keep on it.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
