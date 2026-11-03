namespace CrestApps.OrchardCore.Accounts;

/// <summary>
/// The names the Accounts module and the modules that extend an account share.
/// </summary>
public static class AccountsConstants
{
    /// <summary>
    /// The id of the Accounts feature.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Accounts";

    /// <summary>
    /// The technical names of the content parts an account is made of.
    /// </summary>
    public static class ContentParts
    {
        /// <summary>
        /// The part that makes a content type an account type.
        /// </summary>
        public const string Account = "AccountPart";

        /// <summary>
        /// Orchard Core's list part, which holds an account's contacts.
        /// </summary>
        public const string List = "ListPart";
    }

    /// <summary>
    /// The technical names of the content types the Accounts module creates.
    /// </summary>
    public static class ContentTypes
    {
        /// <summary>
        /// The account type created when the feature is enabled.
        /// </summary>
        public const string Account = "Account";
    }

    /// <summary>
    /// The names of the routes the Accounts module maps.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The account search behind the account pickers.
        /// </summary>
        public const string Search = "CrestApps.Accounts.Search";
    }
}
