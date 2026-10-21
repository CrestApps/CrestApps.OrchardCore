namespace CrestApps.OrchardCore.Companies;

public static class CompaniesConstants
{
    public const string Feature = "CrestApps.OrchardCore.Companies";

    /// <summary>
    /// The YesSql collection companies and their price lists live in.
    /// </summary>
    public const string CollectionName = "Companies";

    /// <summary>
    /// The tender a company's order is paid on its terms by: what its deposit doesn't cover is owed, on an invoice due
    /// in the company's net days.
    /// </summary>
    public const string TermsTenderKey = "company-terms";

    /// <summary>
    /// The source of the transactions a company owes on its terms.
    /// </summary>
    public const string TransactionSource = "company-terms";
}
