using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Works out a document's tax from the taxes the rules found for its items, with EN 16931 arithmetic: tax per tax and
/// rate on the summed net, spread back over the items largest remainder first; a compound tax on the net plus the other
/// taxes; prices that include tax split once per group of items that share their taxes; each item's own treatment and
/// reason, else the document's; and cash rounding as an explicit amount. Invoices and credit notes use it; the
/// checkout's tax service rounds per rate the same way (<see cref="TaxRoundingLevel.Rate"/>), so a cart and an invoice
/// with the same lines come to the same tax.
/// </summary>
public interface ITaxBreakdownCalculator
{
    /// <summary>
    /// Works out the breakdown of the supplied document.
    /// </summary>
    /// <param name="request">The document's items and how its prices are given.</param>
    TaxBreakdownResult Calculate(TaxBreakdownRequest request);
}
