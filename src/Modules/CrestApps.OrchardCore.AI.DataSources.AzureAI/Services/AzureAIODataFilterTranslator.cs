using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.AI.DataSources.AzureAI.Services;

/// <summary>
/// Translates OData filter expressions for Azure AI Search.
/// Since Azure AI Search natively supports OData, this translator
/// simply prefixes field names with "filters/" to target the filter fields
/// in the knowledge base index.
/// </summary>
internal sealed class AzureAIODataFilterTranslator : IODataFilterTranslator
{
    public string Translate(string odataFilter)
    {
        if (string.IsNullOrWhiteSpace(odataFilter))
        {
            return null;
        }

        // Azure AI Search uses OData natively.
        // Prefix field names with "filters/" to target the correct fields.
        // Note: Azure AI Search uses "/" as nested field separator.
        return PrefixFieldNames(odataFilter);
    }

    private static string PrefixFieldNames(string filter)
    {
        // Simple approach: identify unquoted identifiers and prefix them.
        // OData field names appear before operators (eq, ne, gt, lt, ge, le)
        // and inside function calls (contains, startswith, endswith).
        var result = new System.Text.StringBuilder();
        var i = 0;

        while (i < filter.Length)
        {
            // Skip quoted strings.
            if (filter[i] == '\'')
            {
                var end = filter.IndexOf('\'', i + 1);
                if (end < 0)
                {
                    end = filter.Length - 1;
                }

                result.Append(filter, i, end - i + 1);
                i = end + 1;
                continue;
            }

            // Identify tokens.
            if (char.IsLetter(filter[i]) || filter[i] == '_')
            {
                var start = i;

                while (i < filter.Length && (char.IsLetterOrDigit(filter[i]) || filter[i] == '_' || filter[i] == '.'))
                {
                    i++;
                }

                var token = filter[start..i];

                // Don't prefix OData keywords and function names.
                if (IsODataKeyword(token))
                {
                    result.Append(token);
                }
                else
                {
                    // This is a field name — prefix with filters/.
                    if (!token.StartsWith($"{DataSourceConstants.ColumnNames.Filters}/", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Append($"{DataSourceConstants.ColumnNames.Filters}/{token}");
                    }
                    else
                    {
                        result.Append(token);
                    }
                }
            }
            else
            {
                result.Append(filter[i]);
                i++;
            }
        }

        return result.ToString();
    }

    private static bool IsODataKeyword(string token)
    {
        return token switch
        {
            "eq" or "ne" or "gt" or "lt" or "ge" or "le" or
            "and" or "or" or "not" or
            "contains" or "startswith" or "endswith" or
            "true" or "false" or "null" or
            "in" => true,
            _ => false,
        };
    }
}
