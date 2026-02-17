using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.DataSources.Elasticsearch.Services;

/// <summary>
/// Translates OData filter expressions into Elasticsearch bool query JSON
/// targeting the "filters." prefixed fields in the knowledge base index.
/// Supports: eq, ne, gt, lt, ge, le, and, or, not, contains, startswith, endswith.
/// </summary>
internal sealed partial class ElasticsearchODataFilterTranslator : IODataFilterTranslator
{
    public string Translate(string odataFilter)
    {
        if (string.IsNullOrWhiteSpace(odataFilter))
        {
            return null;
        }

        var tokens = Tokenize(odataFilter);

        if (tokens.Count == 0)
        {
            return null;
        }

        var index = 0;
        var result = ParseExpression(tokens, ref index);

        return result;
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var regex = TokenRegex();

        foreach (Match match in regex.Matches(input))
        {
            tokens.Add(match.Value);
        }

        return tokens;
    }

    private static string ParseExpression(List<string> tokens, ref int index)
    {
        var left = ParseUnary(tokens, ref index);

        while (index < tokens.Count)
        {
            var token = tokens[index];

            if (string.Equals(token, "and", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                var right = ParseUnary(tokens, ref index);
                left = $"{{\"bool\":{{\"must\":[{left},{right}]}}}}";
            }
            else if (string.Equals(token, "or", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                var right = ParseUnary(tokens, ref index);
                left = $"{{\"bool\":{{\"should\":[{left},{right}],\"minimum_should_match\":1}}}}";
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private static string ParseUnary(List<string> tokens, ref int index)
    {
        if (index < tokens.Count && string.Equals(tokens[index], "not", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            var operand = ParsePrimary(tokens, ref index);
            return $"{{\"bool\":{{\"must_not\":[{operand}]}}}}";
        }

        return ParsePrimary(tokens, ref index);
    }

    private static string ParsePrimary(List<string> tokens, ref int index)
    {
        if (index >= tokens.Count)
        {
            return "{}";
        }

        // Parenthesized expression.
        if (tokens[index] == "(")
        {
            index++; // skip (
            var result = ParseExpression(tokens, ref index);
            if (index < tokens.Count && tokens[index] == ")")
            {
                index++; // skip )
            }

            return result;
        }

        // Function-style: contains(field, 'value'), startswith(...), endswith(...)
        if (index + 1 < tokens.Count && tokens[index + 1] == "(")
        {
            var funcName = tokens[index].ToLowerInvariant();
            index += 2; // skip funcName and (

            var field = PrefixField(tokens[index]);
            index++;

            // skip comma
            if (index < tokens.Count && tokens[index] == ",")
            {
                index++;
            }

            var value = UnquoteValue(tokens[index]);
            index++;

            // skip )
            if (index < tokens.Count && tokens[index] == ")")
            {
                index++;
            }

            return funcName switch
            {
                "contains" => $"{{\"wildcard\":{{\"{field}\":{{\"value\":\"*{EscapeWildcard(value)}*\"}}}}}}",
                "startswith" => $"{{\"prefix\":{{\"{field}\":{{\"value\":\"{EscapeJson(value)}\"}}}}}}",
                "endswith" => $"{{\"wildcard\":{{\"{field}\":{{\"value\":\"*{EscapeWildcard(value)}\"}}}}}}",
                _ => "{}",
            };
        }

        // Binary comparison: field op value
        var fieldToken = tokens[index];
        index++;

        if (index >= tokens.Count)
        {
            return "{}";
        }

        var op = tokens[index].ToLowerInvariant();
        index++;

        if (index >= tokens.Count)
        {
            return "{}";
        }

        var valueToken = tokens[index];
        index++;

        var prefixedField = PrefixField(fieldToken);
        var parsedValue = UnquoteValue(valueToken);

        return op switch
        {
            "eq" => $"{{\"term\":{{\"{prefixedField}\":\"{EscapeJson(parsedValue)}\"}}}}",
            "ne" => $"{{\"bool\":{{\"must_not\":[{{\"term\":{{\"{prefixedField}\":\"{EscapeJson(parsedValue)}\"}}}}]}}}}",
            "gt" => $"{{\"range\":{{\"{prefixedField}\":{{\"gt\":\"{EscapeJson(parsedValue)}\"}}}}}}",
            "ge" => $"{{\"range\":{{\"{prefixedField}\":{{\"gte\":\"{EscapeJson(parsedValue)}\"}}}}}}",
            "lt" => $"{{\"range\":{{\"{prefixedField}\":{{\"lt\":\"{EscapeJson(parsedValue)}\"}}}}}}",
            "le" => $"{{\"range\":{{\"{prefixedField}\":{{\"lte\":\"{EscapeJson(parsedValue)}\"}}}}}}",
            _ => "{}",
        };
    }

    private static string PrefixField(string field)
    {
        if (field.StartsWith($"{DataSourceConstants.ColumnNames.Filters}.", StringComparison.OrdinalIgnoreCase))
        {
            return field;
        }

        return $"{DataSourceConstants.ColumnNames.Filters}.{field}";
    }

    private static string UnquoteValue(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1];
        }

        return value;
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string EscapeWildcard(string value)
    {
        return EscapeJson(value)
            .Replace("*", "\\*")
            .Replace("?", "\\?");
    }

    [GeneratedRegex(@"'[^']*'|[(),]|\w[\w.]*")]
    private static partial Regex TokenRegex();
}
