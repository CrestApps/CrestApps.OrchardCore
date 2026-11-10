namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// A structured field item (RFC 8941 section 3.3): a bare value with its parameters.
/// </summary>
public sealed class StructuredFieldItem
{
    private static readonly IReadOnlyDictionary<string, object> _noParameters = new Dictionary<string, object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldItem"/> class.
    /// </summary>
    /// <param name="value">The bare value: a <see cref="string"/> (sf-string or token), a <see cref="byte"/> array, a <see cref="long"/>, a <see cref="decimal"/> or a <see cref="bool"/>.</param>
    /// <param name="isToken">Whether a string value is a token rather than an sf-string.</param>
    /// <param name="parameters">The parameters, in order, or <see langword="null"/> for none.</param>
    public StructuredFieldItem(object value, bool isToken = false, IReadOnlyDictionary<string, object> parameters = null)
    {
        Value = value;
        IsToken = isToken;
        Parameters = parameters ?? _noParameters;
    }

    /// <summary>
    /// Gets the bare value: a <see cref="string"/> for sf-strings and tokens, a <see cref="byte"/> array for byte
    /// sequences, a <see cref="long"/> for integers, a <see cref="decimal"/> for decimals, or a <see cref="bool"/>.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Gets a value indicating whether a string <see cref="Value"/> is a token rather than an sf-string.
    /// </summary>
    public bool IsToken { get; }

    /// <summary>
    /// Gets the item's parameters in order. Token values are <see cref="StructuredFieldToken"/> instances; the other
    /// values have the same types as <see cref="Value"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }
}
