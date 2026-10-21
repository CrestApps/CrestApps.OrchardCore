using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// How an order line reads in one line of text, on an invoice or in an email: its title, the price chosen, and its
/// add-ons, such as "Mug (Large) · Name: Sam · Gift wrap".
/// </summary>
public static class OrderLineText
{
    public static string Describe(OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var text = string.IsNullOrEmpty(line.PriceName) ? line.Title : $"{line.Title} ({line.PriceName})";

        return line.AddOns is { Count: > 0 }
            ? text + " · " + string.Join(" · ", line.AddOns.Select(DescribeAddOn))
            : text;
    }

    /// <summary>
    /// Returns what a bundle line holds in all, such as "2 × Tee, 1 × Cap" for one bundle, or
    /// <see langword="null"/> for a line that isn't a bundle.
    /// </summary>
    public static string DescribeBundle(OrderLine line)
        => line?.BundleItems is { Count: > 0 } items
            ? string.Join(", ", items.Select(item => $"{item.Quantity * Math.Max(1, line.Quantity)} × {item.Title}"))
            : null;

    /// <summary>
    /// How a line reads to whoever packs it: its description and, for a bundle, what to put in the box.
    /// </summary>
    public static string DescribeForPacking(OrderLine line)
        => DescribeBundle(line) is { } contents ? $"{Describe(line)} ({contents})" : Describe(line);

    public static string DescribeAddOn(OrderLineAddOn addOn)
        => string.IsNullOrEmpty(addOn.Value) ? addOn.Label : $"{addOn.Label}: {addOn.Value}";
}
