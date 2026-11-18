using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.BusinessDocuments.Deployments;

/// <summary>
/// Exports the document templates.
/// </summary>
public sealed class BusinessDocumentTemplatesDeploymentStep : DeploymentStep
{
    /// <summary>
    /// The name of the step, and of the recipe step that imports it.
    /// </summary>
    public const string StepName = "BusinessDocumentTemplates";

    /// <summary>
    /// The name of the member of the recipe step that holds the templates.
    /// </summary>
    public const string EntriesName = "Templates";

    private static readonly LocalizationSource _category = LocalizationSource.Create<BusinessDocumentTemplatesDeploymentStep>("Business documents");
    private static readonly LocalizationSource _title = LocalizationSource.Create<BusinessDocumentTemplatesDeploymentStep>("Document templates");

    /// <inheritdoc />
    public override string Name => StepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
