namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Reads values from parsed structured field dictionaries, such as the <c>profile</c> of a
/// <c>UCP-Agent: profile="https://..."</c> header.
/// </summary>
public static class StructuredFieldDictionaryExtensions
{
    /// <summary>
    /// Gets the string or token value of a dictionary member that is an item.
    /// </summary>
    /// <param name="dictionary">The parsed dictionary.</param>
    /// <param name="key">The member key.</param>
    /// <returns>The string, or <see langword="null"/> when the member is absent, an inner list or not a string.</returns>
    public static string GetString(this IReadOnlyDictionary<string, StructuredFieldMember> dictionary, string key)
    {
        if (dictionary is null || key is null || !dictionary.TryGetValue(key, out var member) || member.IsInnerList)
        {
            return null;
        }

        return member.Item.Value as string;
    }
}
