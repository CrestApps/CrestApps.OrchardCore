namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The tax categories every country has; a country pack adds its own with <c>AddIncomeTaxCategory</c>. A category is
/// the base of a tax: income that counts for one category may not count for another (a benefit can be taxable income
/// but not insurable earnings).
/// </summary>
public static class IncomeTaxCategories
{
    /// <summary>Income tax withheld.</summary>
    public const string IncomeTax = "IncomeTax";

    /// <summary>Social insurance (pensions, health): pensionable earnings, paid by the earner, the payer or both.</summary>
    public const string SocialInsurance = "SocialInsurance";

    /// <summary>Unemployment or employment insurance: insurable earnings.</summary>
    public const string Unemployment = "Unemployment";

    /// <summary>
    /// Withholding on a contractor's payments (backup withholding in the United States), registered by the country pack
    /// that has it.
    /// </summary>
    public const string ContractorWithholding = "ContractorWithholding";
}

/// <summary>
/// Who pays a tax on income.
/// </summary>
public enum IncomeTaxPayer
{
    /// <summary>The earner: it is taken from the income.</summary>
    Employee,

    /// <summary>The payer (the employer): it is paid on top of the income.</summary>
    Employer,
}

/// <summary>
/// The kind of income a tax is worked out on.
/// </summary>
public enum IncomeKind
{
    /// <summary>Employment income: wages, salary, commissions.</summary>
    Employment,

    /// <summary>A contractor's payments.</summary>
    Contractor,

    /// <summary>A pension or an annuity.</summary>
    Pension,

    /// <summary>Other income.</summary>
    Other,
}

/// <summary>
/// How supplemental income (a bonus, a commission paid on its own, retroactive pay) is withheld on.
/// </summary>
public enum SupplementalWithholdingMethod
{
    /// <summary>With the regular income of the period, or as a country's withholding method says.</summary>
    Aggregate,

    /// <summary>At the tax's flat supplemental rate.</summary>
    Flat,
}

/// <summary>
/// Whose home or place of work an income tax rule applies to, for a rule below a country (such as a city's tax at one
/// rate on its residents and another on the people who only work there).
/// </summary>
public enum IncomeTaxResidency
{
    /// <summary>Anyone in the jurisdiction, by home or by place of work.</summary>
    Any,

    /// <summary>Only a person whose home is in the jurisdiction.</summary>
    Resident,

    /// <summary>Only a person who works in the jurisdiction and whose home is not in it.</summary>
    Nonresident,
}
