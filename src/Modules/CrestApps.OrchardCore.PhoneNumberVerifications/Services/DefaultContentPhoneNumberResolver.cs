using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Default <see cref="IContentPhoneNumberResolver"/> that scans the content item for the
/// first field whose name contains "phone" and exposes a textual value. Other modules can
/// register a more specific resolver to take precedence.
/// </summary>
public sealed class DefaultContentPhoneNumberResolver : IContentPhoneNumberResolver
{
    /// <inheritdoc/>
    public Task<string> GetPhoneNumberAsync(ContentItem contentItem)
    {
        if (contentItem is null)
        {
            return Task.FromResult<string>(null);
        }

        var content = (JsonObject)contentItem.Content;

        if (content is null)
        {
            return Task.FromResult<string>(null);
        }

        foreach (var part in content)
        {
            if (part.Value is not JsonObject partObject)
            {
                continue;
            }

            foreach (var field in partObject)
            {
                if (field.Key.Contains("phone", StringComparison.OrdinalIgnoreCase) != true)
                {
                    continue;
                }

                var value = ExtractValue(field.Value);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Task.FromResult(value.Trim());
                }
            }
        }

        return Task.FromResult<string>(null);
    }

    private static string ExtractValue(JsonNode node)
    {
        if (node is JsonValue scalar)
        {
            return scalar.GetValue<object>()?.ToString();
        }

        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("Text", out var text) && text is JsonValue textValue)
            {
                return textValue.GetValue<object>()?.ToString();
            }

            if (obj.TryGetPropertyValue("Value", out var value) && value is JsonValue rawValue)
            {
                return rawValue.GetValue<object>()?.ToString();
            }
        }

        return null;
    }
}
