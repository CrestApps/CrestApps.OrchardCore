using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Shared JSON and base64url helpers for the JOSE, SD-JWT and HTTP signature code.
/// </summary>
internal static class JoseJson
{
    private static readonly JsonSerializerOptions _compactOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly JsonDocumentOptions _strictDocumentOptions = new()
    {
        AllowDuplicateProperties = false,
    };

    public static string Serialize(JsonNode node)
    {
        return node is null
            ? "null"
            : node.ToJsonString(_compactOptions);
    }

    public static byte[] SerializeToUtf8(JsonNode node)
    {
        return Encoding.UTF8.GetBytes(Serialize(node));
    }

    public static string EncodeSegment(JsonNode node)
    {
        return Base64Url.EncodeToString(SerializeToUtf8(node));
    }

    public static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = null;

        if (value is null)
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(value);

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool TryParseNode(ReadOnlySpan<byte> utf8, out JsonNode node)
    {
        node = null;

        try
        {
            node = JsonNode.Parse(utf8, documentOptions: _strictDocumentOptions);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool TryParseObject(ReadOnlySpan<byte> utf8, out JsonObject value)
    {
        value = null;

        if (!TryParseNode(utf8, out var node) || node is not JsonObject obj)
        {
            return false;
        }

        value = obj;

        return true;
    }

    public static bool TryDecodeObjectSegment(string segment, out JsonObject value)
    {
        value = null;

        return TryDecodeBase64Url(segment, out var bytes) && TryParseObject(bytes, out value);
    }

    public static string GetString(JsonObject obj, string name)
    {
        if (obj is null || !obj.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
        {
            return null;
        }

        if (value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        return value.TryGetValue<string>(out var text)
            ? text
            : null;
    }

    public static bool TryGetNumber(JsonNode node, out double number)
    {
        number = 0;

        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.Number)
        {
            return false;
        }

        return double.TryParse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    public static bool IsJsonString(JsonNode node, out string text)
    {
        text = null;

        if (node is not JsonValue value || value.GetValueKind() != JsonValueKind.String)
        {
            return false;
        }

        return value.TryGetValue(out text);
    }
}
