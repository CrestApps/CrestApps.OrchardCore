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
    /// The member of the <see cref="TaxCategory"/> step that holds the tax categories.
    /// </summary>
    public const string TaxCategoryEntries = "TaxCategories";

    /// <summary>
    /// The recipe step that carries tax jurisdictions.
    /// </summary>
    public const string TaxJurisdiction = "TaxJurisdiction";

    /// <summary>
    /// The member of the <see cref="TaxJurisdiction"/> step that holds the tax jurisdictions.
    /// </summary>
    public const string TaxJurisdictionEntries = "TaxJurisdictions";

    /// <summary>
    /// The recipe step that carries tax rules.
    /// </summary>
    public const string TaxRule = "TaxRule";

    /// <summary>
    /// The member of the <see cref="TaxRule"/> step that holds the tax rules.
    /// </summary>
    public const string TaxRuleEntries = "TaxRules";

    /// <summary>
    /// The recipe step that carries tax types.
    /// </summary>
    public const string TaxType = "TaxType";

    /// <summary>
    /// The member of the <see cref="TaxType"/> step that holds the tax types.
    /// </summary>
    public const string TaxTypeEntries = "TaxTypes";

    /// <summary>
    /// The recipe step that carries tax tables.
    /// </summary>
    public const string TaxTable = "TaxTable";

    /// <summary>
    /// The member of the <see cref="TaxTable"/> step that holds the tax tables.
    /// </summary>
    public const string TaxTableEntries = "TaxTables";
}
