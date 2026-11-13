namespace CrestApps.OrchardCore.PointOfSale;

public static class PosConstants
{
    public const string Feature = "CrestApps.OrchardCore.PointOfSale";

    /// <summary>
    /// The feature that lets staff ring up sales in the browser, on the site's own register page.
    /// </summary>
    public const string WebRegisterFeature = "CrestApps.OrchardCore.PointOfSale.WebRegister";

    /// <summary>
    /// The content type an order line rung up in a department carries, in place of a product's: its content item id is
    /// the department's id.
    /// </summary>
    public const string DepartmentLineType = "PosDepartment";

    /// <summary>
    /// The YesSql collection registers and shifts live in.
    /// </summary>
    public const string CollectionName = "PointOfSale";

    /// <summary>
    /// The tender a sale's cash is taken by. Its reference is the cash handed over, in the currency's minor units.
    /// </summary>
    public const string CashTenderKey = "cash";

    /// <summary>
    /// The payment method for a card taken on the store's own card terminal, outside the site.
    /// </summary>
    public const string CounterCardProviderKey = "pos-card";

    /// <summary>
    /// The payment method for a card presented on a Stripe Terminal reader, when the Stripe module is on.
    /// </summary>
    public const string TerminalProviderKey = "stripe-terminal";

    /// <summary>
    /// The tag the point-of-sale endpoints are documented under.
    /// </summary>
    public const string ApiTag = "Point of Sale";

    public static class Recipes
    {
        /// <summary>
        /// The recipe step that adds or updates registers.
        /// </summary>
        public const string Registers = "Registers";

        /// <summary>
        /// The recipe step that adds or updates the departments registers ring amounts up in.
        /// </summary>
        public const string Departments = "PosDepartments";
    }
}
