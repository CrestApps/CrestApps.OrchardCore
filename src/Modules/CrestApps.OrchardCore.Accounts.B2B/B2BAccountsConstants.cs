namespace CrestApps.OrchardCore.Accounts.B2B;

public static class B2BAccountsConstants
{
    public const string Feature = "CrestApps.OrchardCore.Accounts.B2B";

    /// <summary>
    /// The YesSql collection price lists live in.
    /// </summary>
    public const string PriceListsCollectionName = "PriceLists";

    /// <summary>
    /// The tender an account's order is paid on its terms by: what its deposit doesn't cover is owed, on an invoice due
    /// in the account's net days.
    /// </summary>
    public const string TermsTenderKey = "account-terms";

    /// <summary>
    /// The source of the transactions an account owes on its terms.
    /// </summary>
    public const string TransactionSource = "account-terms";

    public static class ContentParts
    {
        /// <summary>
        /// The part that lets an account buy as a business.
        /// </summary>
        public const string B2BAccount = "B2BAccountPart";
    }
}
