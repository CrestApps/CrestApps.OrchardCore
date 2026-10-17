namespace CrestApps.OrchardCore.Transactions.Core;

/// <summary>
/// The options of the pages that show a customer their own transactions. They are read from the tenant's
/// <c>CrestApps:Transactions:CustomerPages</c> configuration section, and a module can set them in code with
/// <c>services.Configure&lt;TransactionCustomerPagesOptions&gt;(...)</c>.
/// </summary>
public sealed class TransactionCustomerPagesOptions
{
    /// <summary>
    /// Gets or sets which pages the links in receipt and reminder messages open: the customer pages in the admin
    /// (<see cref="TransactionCustomerLinkTarget.Admin"/>, the default) or the customer pages in the site theme
    /// (<see cref="TransactionCustomerLinkTarget.Site"/>).
    /// </summary>
    public TransactionCustomerLinkTarget LinkTarget { get; set; } = TransactionCustomerLinkTarget.Admin;
}
