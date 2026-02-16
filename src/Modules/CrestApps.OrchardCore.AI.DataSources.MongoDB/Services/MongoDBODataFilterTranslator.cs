using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.DataSources.MongoDB.Services;

/// <summary>
/// Translates OData filter expressions into MongoDB Atlas Vector Search
/// filter documents (BSON-compatible JSON) targeting the "filters." prefixed
/// fields in the knowledge base index.
/// Supports: eq, ne, gt, lt, ge, le, and, or, not, contains, startswith, endswith, in.
/// </summary>
internal sealed partial class MongoDBODataFilterTranslator : IODataFilterTranslator
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
            var op = tokens[index].ToLowerInvariant();

            if (op == "and")
            {
                index++;
                var right = ParseUnary(tokens, ref index);
                left = $"{{\"compound\":{{\"must\":[{left},{right}]}}}}";
            }
            else if (op == "or")
            {
                index++;
                var right = ParseUnary(tokens, ref index);
                left = $"{{\"compound\":{{\"should\":[{left},{right}],\"minimumShouldMatch\":1}}}}";
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
        if (index < tokens.Count && tokens[index].Equals("not", StringComparison.OrdinalIgnoreCase))
        {
            index++;
            var operand = ParsePrimary(tokens, ref index);
            return $"{{\"compound\":{{\"mustNot\":[{operand}]}}}}";
        }

        return ParsePrimary(tokens, ref index);
    }

    private static string ParsePrimary(List<string> tokens, ref int index)
    {
        if (index >= tokens.Count)
        {
            return "{}";
        }

        // Handle parenthesized expressions.
        if (tokens[index] == "(")
        {
            index++; // skip '('
            var result = ParseExpression(tokens, ref index);

            if (index < tokens.Count && tokens[index] == ")")
            {
                index++; // skip ')'
            }

            return result;
        }

        // Handle function calls: contains(), startswith(), endswith().
        var token = tokens[index].ToLowerInvariant();

        if (token is "contains" or "startswith" or "endswith")
        {
            return ParseFunctionCall(tokens, ref index);
        }

        // Handle comparison: field op value.
        if (index + 2 < tokens.Count)
        {
            var field = PrefixField(tokens[index]);
            var op = tokens[index + 1].ToLowerInvariant();
            var value = tokens[index + 2];
            index += 3;

            return BuildComparison(field, op, value);
        }

        index++;
        return "{}";
    }

    private static string ParseFunctionCall(List<string> tokens, ref int index)
    {
        var function = tokens[index].ToLowerInvariant();
        index++; // skip function name

        if (index < tokens.Count && tokens[index] == "(")
        {
            index++; // skip '('
        }

        // Parse first argument (field).
        var field = index < tokens.Count ? PrefixField(tokens[index]) : "";
        index++;

        // Skip comma.
        if (index < tokens.Count && tokens[index] == ",")
        {
            index++;
        }

        // Parse second argument (value).
        var value = index < tokens.Count ? tokens[index] : "";
        index++;

        // Skip closing ')'.
        if (index < tokens.Count && tokens[index] == ")")
        {
            index++;
        }

        var cleanValue = StripQuotes(value);

        // MongoDB Atlas Vector Search supports "text" filter for string matching.
        // For contains/startswith/endswith, we use regex in the equals filter path.
        // Atlas vector search filter supports: equals, in, range — not regex.
        // Best approximation: use "equals" for exact match scenarios.
        // For true substring search, this would need a text index, which is outside vector search scope.
        return function switch
        {
            "contains" => $"{{\"text\":{{\"path\":\"{field}\",\"query\":\"{EscapeJson(cleanValue)}\"}}}}",
            "startswith" => $"{{\"text\":{{\"path\":\"{field}\",\"query\":\"{EscapeJson(cleanValue)}\"}}}}",
            "endswith" => $"{{\"text\":{{\"path\":\"{field}\",\"query\":\"{EscapeJson(cleanValue)}\"}}}}",
            _ => "{}",
        };
    }

    private static string BuildComparison(string field, string op, string rawValue)
    {
        var value = StripQuotes(rawValue);
        var isNumeric = double.TryParse(value, out var numValue);
        var isBool = bool.TryParse(value, out var boolValue);

        return op switch
        {
            "eq" when isBool => $"{{\"equals\":{{\"path\":\"{field}\",\"value\":{value.ToLowerInvariant()}}}}}",
            "eq" when isNumeric => $"{{\"equals\":{{\"path\":\"{field}\",\"value\":{numValue}}}}}",
            "eq" => $"{{\"equals\":{{\"path\":\"{field}\",\"value\":\"{EscapeJson(value)}\"}}}}",

            "ne" when isBool => $"{{\"compound\":{{\"mustNot\":[{{\"equals\":{{\"path\":\"{field}\",\"value\":{value.ToLowerInvariant()}}}}}]}}}}",
            "ne" when isNumeric => $"{{\"compound\":{{\"mustNot\":[{{\"equals\":{{\"path\":\"{field}\",\"value\":{numValue}}}}}]}}}}",
            "ne" => $"{{\"compound\":{{\"mustNot\":[{{\"equals\":{{\"path\":\"{field}\",\"value\":\"{EscapeJson(value)}\"}}}}]}}}}",

            "gt" when isNumeric => $"{{\"range\":{{\"path\":\"{field}\",\"gt\":{numValue}}}}}",
            "gt" => $"{{\"range\":{{\"path\":\"{field}\",\"gt\":\"{EscapeJson(value)}\"}}}}",

            "ge" when isNumeric => $"{{\"range\":{{\"path\":\"{field}\",\"gte\":{numValue}}}}}",
            "ge" => $"{{\"range\":{{\"path\":\"{field}\",\"gte\":\"{EscapeJson(value)}\"}}}}",

            "lt" when isNumeric => $"{{\"range\":{{\"path\":\"{field}\",\"lt\":{numValue}}}}}",
            "lt" => $"{{\"range\":{{\"path\":\"{field}\",\"lt\":\"{EscapeJson(value)}\"}}}}",

            "le" when isNumeric => $"{{\"range\":{{\"path\":\"{field}\",\"lte\":{numValue}}}}}",
            "le" => $"{{\"range\":{{\"path\":\"{field}\",\"lte\":\"{EscapeJson(value)}\"}}}}",

            "in" => BuildInFilter(field, rawValue),

            _ => "{}",
        };
    }

    private static string BuildInFilter(string field, string rawValue)
    {
        // OData "in" syntax: field in ('val1', 'val2', 'val3')
        var cleaned = rawValue.Trim('(', ')');
        var values = cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var sb = new StringBuilder();
        sb.Append($"{{\"in\":{{\"path\":\"{field}\",\"value\":[");

        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var val = StripQuotes(values[i].Trim());

            if (double.TryParse(val, out var numVal))
            {
                sb.Append(numVal);
            }
            else
            {
                sb.Append($"\"{EscapeJson(val)}\"");
            }
        }

        sb.Append("]}}");

        return sb.ToString();
    }

    private static string PrefixField(string field)
    {
        var prefix = $"{DataSourceConstants.ColumnNames.Filters}.";

        if (field.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return field;
        }

        return $"{prefix}{field}";
    }

    private static string StripQuotes(string value)
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
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    [GeneratedRegex(@"'[^']*'|[(),]|\S+")]
    private static partial Regex TokenRegex();
}
