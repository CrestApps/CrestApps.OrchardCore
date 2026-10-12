using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Products;

/// <summary>
/// Provides Products-module-specific constants for the currency management screens.
/// </summary>
public static class ProductsConstants
{
    /// <summary>
    /// Contains recipe step names.
    /// </summary>
    public static class Recipes
    {
        /// <summary>
        /// The recipe step name used to import managed currencies.
        /// </summary>
        public const string Currencies = "Currencies";
    }

    /// <summary>
    /// Contains permissions exposed by the Products module.
    /// </summary>
    public static class Permissions
    {
        /// <summary>
        /// Gets the permission that allows administrators to manage currencies.
        /// </summary>
        public static readonly Permission ManageCurrencies = new("ManageCurrencies", "Manage currencies");
    }
}
