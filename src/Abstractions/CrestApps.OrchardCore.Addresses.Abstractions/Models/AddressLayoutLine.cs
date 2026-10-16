namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// One line of a country's address layout: components and the text the country writes between them, such as the
/// <c>, </c> in <c>Tokyo, Tokyo-to</c> or the <c>〒</c> before a Japanese postal code.
/// </summary>
public sealed class AddressLayoutLine
{
    public AddressLayoutLine(IReadOnlyList<AddressLayoutToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        Tokens = tokens;
    }

    public IReadOnlyList<AddressLayoutToken> Tokens { get; }

    /// <summary>
    /// Gets the components on the line, in order.
    /// </summary>
    public IEnumerable<AddressComponent> Components
        => Tokens.Where(token => token.Component.HasValue).Select(token => token.Component.Value);
}

/// <summary>
/// A component, or the literal text between components.
/// </summary>
public readonly record struct AddressLayoutToken(AddressComponent? Component, string Text)
{
    public static AddressLayoutToken For(AddressComponent component)
        => new(component, null);

    public static AddressLayoutToken Literal(string text)
        => new(null, text);
}
