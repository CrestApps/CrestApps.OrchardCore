namespace CrestApps.OrchardCore.AI.Core.Search;

/// <summary>
/// Builds OData filter clauses for search indexes (Azure AI Search) with every value escaped as an OData string
/// literal, so a value that holds a quote cannot end the literal and add clauses of its own.
/// </summary>
public static class ODataFilter
{
    /// <summary>
    /// Returns an OData string literal for a value: the value in single quotes, with each single quote doubled.
    /// </summary>
    /// <param name="value">The value. <see langword="null"/> is treated as an empty string.</param>
    public static string Quote(string value)
        => $"'{(value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal)}'";

    /// <summary>
    /// Returns an equality clause, <c>field eq 'value'</c>, with the value escaped.
    /// </summary>
    /// <param name="field">The field name. It must come from code, never from user input.</param>
    /// <param name="value">The value to compare with.</param>
    public static string Equal(string field, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);

        return $"{field} eq {Quote(value)}";
    }

    /// <summary>
    /// Returns whether a filter, put in parentheses after <c>and</c>, stays inside them: every string literal it opens
    /// is closed, and no parenthesis outside a literal closes before it was opened or stays open. Such a filter can only
    /// narrow the clause before it; one that is not could close the group and add an <c>or</c> of its own.
    /// </summary>
    /// <param name="filter">The filter.</param>
    public static bool IsSelfContained(string filter)
    {
        var depth = 0;
        var inLiteral = false;

        foreach (var character in filter ?? string.Empty)
        {
            if (character == '\'')
            {
                // A doubled quote inside a literal closes and reopens it, which leaves the state as it was.
                inLiteral = !inLiteral;
            }
            else if (!inLiteral && character == '(')
            {
                depth++;
            }
            else if (!inLiteral && character == ')' && --depth < 0)
            {
                return false;
            }
        }

        return !inLiteral && depth == 0;
    }
}
