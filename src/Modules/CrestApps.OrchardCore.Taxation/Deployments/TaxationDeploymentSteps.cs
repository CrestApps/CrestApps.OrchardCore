namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Names the recipe steps that carry taxation configuration between environments.
/// </summary>
public static class TaxationDeploymentSteps
{
    /// <summary>
    /// The recipe step that carries tax categories.
    /// </summary>
    public const string TaxCategory = "TaxCategory";

    /// <summary>
    /// The recipe step that carries tax jurisdictions.
    /// </summary>
    public const string TaxJurisdiction = "TaxJurisdiction";

    /// <summary>
    /// The recipe step that carries tax rules.
    /// </summary>
    public const string TaxRule = "TaxRule";

    /// <summary>
    /// The recipe step that carries tax types.
    /// </summary>
    public const string TaxType = "TaxType";

    /// <summary>
    /// The recipe step that carries tax tables.
    /// </summary>
    public const string TaxTable = "TaxTable";
}
