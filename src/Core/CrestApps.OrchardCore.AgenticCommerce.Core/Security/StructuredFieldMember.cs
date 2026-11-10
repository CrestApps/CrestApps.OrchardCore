namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The value of a structured field dictionary member (RFC 8941 section 3.2): an item or an inner list.
/// </summary>
public sealed class StructuredFieldMember
{
    private static readonly IReadOnlyDictionary<string, object> _noParameters = new Dictionary<string, object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldMember"/> class that holds an item.
    /// </summary>
    /// <param name="item">The item.</param>
    public StructuredFieldMember(StructuredFieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        InnerList = [];
        Parameters = item.Parameters;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldMember"/> class that holds an inner list.
    /// </summary>
    /// <param name="innerList">The items of the inner list.</param>
    /// <param name="parameters">The parameters of the inner list, or <see langword="null"/> for none.</param>
    public StructuredFieldMember(IReadOnlyList<StructuredFieldItem> innerList, IReadOnlyDictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(innerList);

        IsInnerList = true;
        InnerList = innerList;
        Parameters = parameters ?? _noParameters;
    }

    /// <summary>
    /// Gets a value indicating whether the member is an inner list.
    /// </summary>
    public bool IsInnerList { get; }

    /// <summary>
    /// Gets the items of the inner list, or an empty list when the member is an item.
    /// </summary>
    public IReadOnlyList<StructuredFieldItem> InnerList { get; }

    /// <summary>
    /// Gets the item, or <see langword="null"/> when the member is an inner list.
    /// </summary>
    public StructuredFieldItem Item { get; }

    /// <summary>
    /// Gets the parameters of the inner list, or of the item when the member is an item.
    /// </summary>
    public IReadOnlyDictionary<string, object> Parameters { get; }
}
