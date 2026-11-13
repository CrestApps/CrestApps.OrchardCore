using CrestApps.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// An amount rung up in a department as a line of a register sale. It's priced as keyed in, has no item behind it, so
/// no stock is held or counted, and it's taxed by the department's tax category.
/// </summary>
public static class PosDepartmentLines
{
    /// <summary>
    /// The line detail that names the department a line was rung up in.
    /// </summary>
    public const string DepartmentDetail = "pos.department";

    /// <summary>
    /// Returns whether an order line was rung up in a department rather than for a product.
    /// </summary>
    public static bool IsDepartmentLine(OrderLine line)
        => line is not null &&
            string.Equals(line.ContentType, PosConstants.DepartmentLineType, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(line.ContentItemId);

    /// <summary>
    /// Returns a priced line for an amount rung up in a department, as the order takes it from a priced cart.
    /// </summary>
    /// <param name="department">The department.</param>
    /// <param name="unitAmount">What one costs, as keyed in.</param>
    /// <param name="quantity">How many: 3 × 1.50 is three at 1.50.</param>
    /// <param name="currency">The sale's currency.</param>
    /// <param name="now">When it was rung up.</param>
    public static CartLinePricing Price(PosDepartment department, decimal unitAmount, int quantity, string currency, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(department);

        var line = new CartLine
        {
            LineId = UniqueId.GenerateId(),
            ContentItemId = department.ItemId,
            Quantity = quantity,
            AddedUnitPrice = unitAmount,
            AddedUtc = now,
            Details = new Dictionary<string, string>(StringComparer.Ordinal) { [DepartmentDetail] = department.ItemId },
        };

        return new CartLinePricing
        {
            Line = line,
            Product = new SellableProduct
            {
                ContentItemId = department.ItemId,
                ContentType = PosConstants.DepartmentLineType,
                Sku = department.Number,
                Title = department.Name,
                UnitPrice = unitAmount,
                Currency = currency,
                ProductType = ProductType.Undefined,
                TaxCategoryCode = department.TaxCategoryCode,
            },
            Title = department.Name,
            UnitPrice = unitAmount,
            LineTotal = unitAmount * quantity,
            MaxQuantity = quantity,
        };
    }
}
