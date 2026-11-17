namespace CrestApps.OrchardCore.Banking;

/// <summary>
/// Names shared by the Banking module and the modules that build on it.
/// </summary>
public static class BankingConstants
{
    /// <summary>
    /// The YesSql collection that holds bank accounts.
    /// </summary>
    public const string CollectionName = "Banking";

    /// <summary>
    /// The name of the recipe step, and of the deployment step, that carries bank accounts.
    /// </summary>
    public const string RecipeStepName = "BankAccounts";

    /// <summary>
    /// The data protection purpose of bank account numbers. Never change it: numbers stored with it could not be read.
    /// </summary>
    public const string AccountNumberPurpose = "Banking.AccountNumber";

    /// <summary>
    /// The id of the admin menu group bank accounts are in. Modules that pay through bank accounts add their pages to it.
    /// </summary>
    public const string AdminMenuId = "banking";

    /// <summary>
    /// The feature ids of the Banking module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Banking feature: bank accounts.
        /// </summary>
        public const string Banking = "CrestApps.OrchardCore.Banking";
    }

    /// <summary>
    /// The country codes the built-in routing checks cover.
    /// </summary>
    public static class Countries
    {
        /// <summary>
        /// The United States: nine-digit ABA routing numbers with a check digit.
        /// </summary>
        public const string UnitedStates = "US";
    }
}
