namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

/// <summary>
/// Provides basic OData filter syntax validation for Azure AI Search filters.
/// </summary>
public sealed class ODataFilterValidator : IODataFilterValidator
{
    private static readonly string[] ODataOperators =
    [
        " eq ", " ne ", " gt ", " ge ", " lt ", " le ",
        " and ", " or ", " not ",
    ];

    /// <inheritdoc/>
    public bool IsValid(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        // Basic OData filter validation to catch common syntax errors
        // Note: This is not a complete OData parser. The Azure SDK will perform full validation.
        // Limitations:
        // - Does not parse string literals (operators within quotes may cause false positives)
        // - Primarily checks single quotes (Azure AI Search standard for strings)
        // - Requires operators or function calls (simple field references are not valid filters)

        // Check for common OData operators
        var hasOperator = ODataOperators.Any(op => filter.Contains(op, StringComparison.OrdinalIgnoreCase));

        if (!hasOperator)
        {
            // If no operator found, it might be a function call like search.in() or geo.distance()
            // Valid Azure AI Search filters require either operators or function calls with parentheses
            if (!filter.Contains('(') || !filter.Contains(')'))
            {
                return false;
            }

            // Reject filters that are only parentheses with no content
            if (filter.Trim() == "()")
            {
                return false;
            }

            return IsParenthesesBalanced(filter);
        }

        // Check for balanced quotes
        var singleQuotes = filter.Count(c => c == '\'');
        if (singleQuotes % 2 != 0)
        {
            return false;
        }

        return IsParenthesesBalanced(filter);
    }

    private static bool IsParenthesesBalanced(string input)
    {
        var balance = 0;

        foreach (var ch in input)
        {
            if (ch == '(')
            {
                balance++;
            }
            else if (ch == ')')
            {
                balance--;
                if (balance < 0)
                {
                    // Closing parenthesis before opening
                    return false;
                }
            }
        }

        return balance == 0;
    }
}
