using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Banking.Deployments;

/// <summary>
/// Exports the bank accounts: without their numbers, unless <see cref="IncludeSensitiveData"/> is on.
/// </summary>
public sealed class BankAccountsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<BankAccountsDeploymentStep>("Finance");
    private static readonly LocalizationSource _title = LocalizationSource.Create<BankAccountsDeploymentStep>("Bank accounts");

    /// <inheritdoc />
    public override string Name => BankingConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the routing and account numbers are exported in clear text, so the importing site can
    /// protect them with its own keys. Only a user who may reveal bank account numbers can turn it on.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }
}
