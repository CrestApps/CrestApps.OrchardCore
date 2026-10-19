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

    public static string DescribeAddOn(OrderLineAddOn addOn)
        => string.IsNullOrEmpty(addOn.Value) ? addOn.Label : $"{addOn.Label}: {addOn.Value}";
}
