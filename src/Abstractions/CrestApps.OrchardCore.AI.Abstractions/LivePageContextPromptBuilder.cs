using System.Text;
using System.Text.Json;

namespace CrestApps.OrchardCore.AI;

public static class LivePageContextPromptBuilder
{
    public static string Append(string prompt, AIInvocationContext invocationContext)
    {
        if (string.IsNullOrWhiteSpace(prompt) || invocationContext is null)
        {
            return prompt;
        }

        if (!invocationContext.Items.TryGetValue(AIInvocationItemKeys.LivePageContextJson, out var rawContext) ||
            rawContext is not string contextJson ||
            string.IsNullOrWhiteSpace(contextJson))
        {
            return prompt;
        }

        var summary = BuildSummary(contextJson);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return prompt;
        }

        return $"{prompt}\n\n[Current visible page context]\n{summary}\n[/Current visible page context]";
    }

    public static void Store(AIInvocationContext invocationContext, string contextJson)
    {
        if (invocationContext is null || string.IsNullOrWhiteSpace(contextJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(contextJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            invocationContext.Items[AIInvocationItemKeys.LivePageContextJson] = contextJson;
        }
        catch (JsonException)
        {
        }
    }

    internal static string BuildSummary(string contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(contextJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendLine(builder, "URL", GetString(root, "url"));
        AppendLine(builder, "Title", GetString(root, "title"));
        AppendLine(builder, "Frame context", GetBoolean(root, "isParentContext") ? "parent page" : "current page");
        AppendList(builder, "Headings", GetStringArray(root, "headings"), 12, 120);
        AppendLinks(builder, root);
        AppendList(builder, "Visible buttons", GetObjectStringArray(root, "buttons", "text"), 20, 120);
        AppendLine(builder, "Visible text preview", Truncate(GetString(root, "textPreview"), 1500));

        return builder.Length == 0 ? null : builder.ToString().TrimEnd();
    }

    private static void AppendLinks(StringBuilder builder, JsonElement root)
    {
        if (!root.TryGetProperty("links", out var linksElement) || linksElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var count = 0;
        foreach (var link in linksElement.EnumerateArray())
        {
            if (count >= 40)
            {
                break;
            }

            var text = Truncate(GetString(link, "text"), 120);
            var href = Truncate(GetString(link, "href"), 240);
            if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (count == 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("Visible links:");
            }

            builder.Append("- ");
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.Append(text);
            }
            else
            {
                builder.Append("[no text]");
            }

            if (!string.IsNullOrWhiteSpace(href))
            {
                builder.Append(" -> ");
                builder.Append(href);
            }

            var context = Truncate(GetString(link, "context"), 160);
            if (!string.IsNullOrWhiteSpace(context))
            {
                builder.Append(" (context: ");
                builder.Append(context);
                builder.Append(')');
            }

            builder.AppendLine();
            count++;
        }
    }

    private static void AppendList(StringBuilder builder, string label, IEnumerable<string> values, int maxItems, int maxLength)
    {
        if (values is null)
        {
            return;
        }

        var appendedAny = false;
        var count = 0;

        foreach (var value in values)
        {
            var normalizedValue = Truncate(value?.Trim(), maxLength);
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                continue;
            }

            if (!appendedAny)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine($"{label}:");
                appendedAny = true;
            }

            builder.Append("- ");
            builder.AppendLine(normalizedValue);
            count++;

            if (count >= maxItems)
            {
                break;
            }
        }
    }

    private static void AppendLine(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(label);
        builder.Append(": ");
        builder.Append(value.Trim());
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return false;
        }

        return property.GetBoolean();
    }

    private static IEnumerable<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                yield return item.GetString();
            }
        }
    }

    private static IEnumerable<string> GetObjectStringArray(JsonElement element, string propertyName, string nestedPropertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = GetString(item, nestedPropertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = value.Trim();
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
