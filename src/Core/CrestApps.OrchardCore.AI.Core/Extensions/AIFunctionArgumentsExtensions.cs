using System.Text.Json;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

public static class AIFunctionArgumentsExtensions
{
    private readonly static JsonSerializerOptions _caseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryGetFirst(this AIFunctionArguments arguments, string key, out object value)
    {
        return arguments.TryGetValue(key, out value) && value is not null;
    }

    public static T GetFirstValueOrDefault<T>(this AIFunctionArguments arguments, string key, T fallbackValue = default)
    {
        if (arguments.TryGetFirst<T>(key, out var value))
        {
            return value;
        }

        return fallbackValue;
    }

    public static bool TryGetFirstString(this AIFunctionArguments arguments, string key, out string value)
        => arguments.TryGetFirstString(key, false, out value);

    public static bool TryGetFirstString(this AIFunctionArguments arguments, string key, bool allowEmptyString, out string value)
    {
        if (arguments.TryGetFirst(key, out value))
        {
            if (!allowEmptyString && string.IsNullOrEmpty(value))
            {
                value = null;

                return false;
            }

            return true;
        }

        value = null;

        return false;
    }

    public static bool TryGetFirst<T>(this AIFunctionArguments arguments, string key, out T value)
    {
        value = default;

        if (!arguments.TryGetValue(key, out var unsafeValue) || unsafeValue is null)
        {
            return false;
        }

        try
        {
            if (unsafeValue is T alreadyTyped)
            {
                value = alreadyTyped;

                return true;
            }

            if (unsafeValue is JsonElement je)
            {
                value = JsonSerializer.Deserialize<T>(je.GetRawText(), _caseInsensitive);

                return true;
            }

            // Handle nullable types (e.g. int?, DateTime?).
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

            var safeValue = Convert.ChangeType(unsafeValue, targetType);

            value = (T)safeValue;

            return true;
        }
        catch
        {
            return false;
        }
    }
}
