using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Reads the arguments of a Contact Center setup tool and turns the ones given into the changes a catalog record's
/// handler applies: each argument the model sent is checked and written under the record's own property name, and
/// every argument it left out is left out, so an update changes only what was asked.
/// </summary>
internal sealed class ContactCenterToolArguments
{
    private readonly JsonObject _arguments = [];
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterToolArguments"/> class.
    /// </summary>
    /// <param name="arguments">The tool's arguments.</param>
    public ContactCenterToolArguments(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (var (key, value) in arguments)
        {
            _arguments[key] = ToNode(value);
        }
    }

    private ContactCenterToolArguments(JsonObject arguments, string path, List<string> errors)
    {
        _arguments = arguments;
        _path = path;
        Errors = errors;
    }

    /// <summary>
    /// Gets the changes read so far, keyed by the record's property names.
    /// </summary>
    public JsonObject Changes { get; } = [];

    /// <summary>
    /// Gets the problems found in the arguments, as sentences the model can relay.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets whether an argument was sent, even as <see langword="null"/>.
    /// </summary>
    public bool Has(string key)
        => _arguments.ContainsKey(key);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    public string GetString(string key)
        => _arguments.TryGetPropertyValue(key, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    public int? GetInt(string key)
        => TryReadInt(key, out var value) ? value : null;

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <returns>The value, or <see langword="null"/> when it is missing or not a yes or no.</returns>
    public bool? GetBool(string key)
    {
        if (!_arguments.TryGetPropertyValue(key, out var node) || node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        return value.TryGetValue<string>(out var text) && bool.TryParse(text, out flag) ? flag : null;
    }

    /// <summary>
    /// Reads an argument as it was sent.
    /// </summary>
    /// <returns>A copy of the argument, or <see langword="null"/> when it is missing.</returns>
    public JsonNode GetNode(string key)
        => _arguments.TryGetPropertyValue(key, out var node) ? node?.DeepClone() : null;

    /// <summary>
    /// Reads a list of texts, such as IDs.
    /// </summary>
    /// <returns>The trimmed, distinct, non-blank values; <see langword="null"/> when the argument is missing.</returns>
    public List<string> GetStringList(string key)
    {
        if (!_arguments.TryGetPropertyValue(key, out var node))
        {
            return null;
        }

        if (node is null)
        {
            return [];
        }

        if (node is not JsonArray array)
        {
            Errors.Add($"{Label(key)} must be a list.");

            return null;
        }

        return array
            .Select(item => item is JsonValue value && value.TryGetValue<string>(out var text) ? text?.Trim() : null)
            .Where(text => !string.IsNullOrEmpty(text))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Copies a text argument to a property. A blank value clears the property.
    /// </summary>
    public void Text(string key, string property, bool required = false)
    {
        if (!_arguments.ContainsKey(key))
        {
            return;
        }

        var text = GetString(key);

        if (text is null && required)
        {
            Errors.Add($"{Label(key)} cannot be empty.");

            return;
        }

        Changes[property] = text;
    }

    /// <summary>
    /// Copies a whole number argument to a property, checking its range.
    /// </summary>
    public void Int(string key, string property, int? minimum = null, int? maximum = null)
    {
        if (!_arguments.ContainsKey(key))
        {
            return;
        }

        if (!TryReadInt(key, out var value) || value is null)
        {
            Errors.Add($"{Label(key)} must be a whole number.");

            return;
        }

        if (value < minimum || value > maximum)
        {
            Errors.Add(maximum is null
                ? $"{Label(key)} must be at least {minimum}."
                : minimum is null ? $"{Label(key)} must be at most {maximum}." : $"{Label(key)} must be between {minimum} and {maximum}.");

            return;
        }

        Changes[property] = value;
    }

    /// <summary>
    /// Copies a number argument to a property, checking its range.
    /// </summary>
    public void Number(string key, string property, double? minimum = null, double? maximum = null)
    {
        if (!_arguments.ContainsKey(key))
        {
            return;
        }

        double? value = null;

        if (_arguments[key] is JsonValue node)
        {
            if (node.TryGetValue<double>(out var number))
            {
                value = number;
            }
            else if (node.TryGetValue<string>(out var text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                value = number;
            }
        }

        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            Errors.Add($"{Label(key)} must be a number.");

            return;
        }

        if (value < minimum || value > maximum)
        {
            Errors.Add($"{Label(key)} must be between {minimum?.ToString(CultureInfo.InvariantCulture) ?? "any"} and {maximum?.ToString(CultureInfo.InvariantCulture) ?? "any"}.");

            return;
        }

        Changes[property] = value;
    }

    /// <summary>
    /// Copies a yes or no argument to a property.
    /// </summary>
    public void Bool(string key, string property)
    {
        if (!_arguments.ContainsKey(key))
        {
            return;
        }

        var value = GetBool(key);

        if (value is null)
        {
            Errors.Add($"{Label(key)} must be true or false.");

            return;
        }

        Changes[property] = value.Value;
    }

    /// <summary>
    /// Copies a choice argument to a property, by the choice's name.
    /// </summary>
    /// <param name="key">The argument.</param>
    /// <param name="property">The record's property.</param>
    /// <param name="allowed">The choices the admin page offers; all of the enumeration's names when <see langword="null"/>.</param>
    public void Choice<TEnum>(string key, string property, IReadOnlyCollection<TEnum> allowed = null)
        where TEnum : struct, Enum
    {
        if (!_arguments.ContainsKey(key))
        {
            return;
        }

        if (TryReadChoice(key, allowed, out var value))
        {
            Changes[property] = value.ToString();
        }
    }

    /// <summary>
    /// Reads a choice argument by the choice's name.
    /// </summary>
    /// <returns><see langword="true"/> when the argument is missing or valid; otherwise an error is recorded.</returns>
    public bool TryReadChoice<TEnum>(string key, IReadOnlyCollection<TEnum> allowed, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;

        if (!_arguments.ContainsKey(key))
        {
            return true;
        }

        var text = GetString(key);
        var choices = allowed ?? Enum.GetValues<TEnum>();

        if (text is not null &&
            !text.All(char.IsAsciiDigit) &&
            Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) &&
            choices.Contains(parsed))
        {
            value = parsed;

            return true;
        }

        Errors.Add($"{Label(key)} must be one of: {string.Join(", ", choices)}.");

        return false;
    }

    /// <summary>
    /// Copies a list of IDs to a property, replacing the whole list.
    /// </summary>
    public void IdList(string key, string property)
    {
        var values = GetStringList(key);

        if (values is not null)
        {
            Changes[property] = new JsonArray(values.Select(value => (JsonNode)value).ToArray());
        }
    }

    /// <summary>
    /// Reads an argument that is an object, such as a group of settings, with its own reader. The nested reader's
    /// problems are added to this reader's.
    /// </summary>
    /// <returns>The nested reader, or <see langword="null"/> when the argument is missing or not an object.</returns>
    public ContactCenterToolArguments Nested(string key)
    {
        if (!_arguments.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        if (node is not JsonObject json)
        {
            Errors.Add($"{Label(key)} must be an object.");

            return null;
        }

        return new ContactCenterToolArguments((JsonObject)json.DeepClone(), Label(key), Errors);
    }

    /// <summary>
    /// Reads an argument that is a list of objects, each with its own reader. The nested readers' problems are added to
    /// this reader's.
    /// </summary>
    /// <returns>The readers, or <see langword="null"/> when the argument is missing or not a list of objects.</returns>
    public List<ContactCenterToolArguments> NestedList(string key)
    {
        if (!_arguments.TryGetPropertyValue(key, out var node))
        {
            return null;
        }

        if (node is null)
        {
            return [];
        }

        if (node is not JsonArray array || array.Any(item => item is not JsonObject))
        {
            Errors.Add($"{Label(key)} must be a list of objects.");

            return null;
        }

        return array
            .Select((item, index) => new ContactCenterToolArguments((JsonObject)item.DeepClone(), $"{Label(key)}[{index}]", Errors))
            .ToList();
    }

    private string Label(string key)
        => _path is null ? key : $"{_path}.{key}";

    private bool TryReadInt(string key, out int? value)
    {
        value = null;

        if (!_arguments.TryGetPropertyValue(key, out var node) || node is not JsonValue number)
        {
            return false;
        }

        if (number.TryGetValue<int>(out var parsed))
        {
            value = parsed;

            return true;
        }

        if (number.TryGetValue<double>(out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)real;

            return true;
        }

        if (number.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;

            return true;
        }

        return false;
    }

    private static JsonNode ToNode(object value)
        => value switch
        {
            null => null,
            JsonNode node => node.DeepClone(),
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            _ => JsonSerializer.SerializeToNode(value),
        };
}
