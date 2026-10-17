using System.Text;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

/// <summary>
/// Reads the address data's notation: a layout such as <c>%N%n%O%n%A%n%C %S %Z</c>, where <c>%n</c> breaks the line and
/// every other <c>%</c> letter is a component, and component lists such as <c>ACSZ</c>.
/// </summary>
internal static class AddressFormatParser
{
    public static IReadOnlyList<AddressLayoutLine> ParseLayout(string layout)
    {
        var lines = new List<AddressLayoutLine>();

        if (string.IsNullOrEmpty(layout))
        {
            return lines;
        }

        var tokens = new List<AddressLayoutToken>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                tokens.Add(AddressLayoutToken.Literal(literal.ToString()));
                literal.Clear();
            }
        }

        void FlushLine()
        {
            FlushLiteral();

            if (tokens.Count > 0)
            {
                lines.Add(new AddressLayoutLine(tokens.ToArray()));
                tokens.Clear();
            }
        }

        for (var i = 0; i < layout.Length; i++)
        {
            if (layout[i] != '%' || i == layout.Length - 1)
            {
                literal.Append(layout[i]);

                continue;
            }

            var code = layout[++i];

            if (code == 'n')
            {
                FlushLine();

                continue;
            }

            if (ToComponent(code) is { } component)
            {
                FlushLiteral();
                tokens.Add(AddressLayoutToken.For(component));
            }
        }

        FlushLine();

        return lines;
    }

    public static IReadOnlySet<AddressComponent> ParseComponents(string codes)
    {
        var components = new HashSet<AddressComponent>();

        foreach (var code in codes ?? string.Empty)
        {
            if (ToComponent(code) is { } component)
            {
                components.Add(component);
            }
        }

        return components;
    }

    private static AddressComponent? ToComponent(char code)
        => code switch
        {
            'N' => AddressComponent.Name,
            'O' => AddressComponent.Company,
            'A' => AddressComponent.StreetAddress,
            'D' => AddressComponent.DependentLocality,
            'C' => AddressComponent.City,
            'S' => AddressComponent.Region,
            'Z' => AddressComponent.PostalCode,
            'X' => AddressComponent.SortingCode,
            _ => null,
        };
}
