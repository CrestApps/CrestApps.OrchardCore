using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Fluid;
using Fluid.Values;

namespace CrestApps.OrchardCore.Core.Liquid;

/// <summary>
/// Turns records into the plain values a Liquid template reads, such as <c>{{ Order.OrderNumber }}</c>: each record is
/// serialized with its property names as written and its enums as names, then read as dictionaries, lists, numbers,
/// booleans and strings. Used by the templated emails and the reply templates.
/// </summary>
public static class LiquidModelValues
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Creates the Liquid value of a record.
    /// </summary>
    /// <param name="model">The record, or <see langword="null"/>.</param>
    /// <param name="options">The template options.</param>
    /// <returns>The value.</returns>
    public static FluidValue Create(object model, TemplateOptions options = null)
        => model is null ? NilValue.Instance : FluidValue.Create(ToPlain(ToJson(model)), options ?? TemplateOptions.Default);

    /// <summary>
    /// Serializes a record as the templates read it.
    /// </summary>
    /// <param name="model">The record.</param>
    /// <returns>The JSON.</returns>
    public static JsonNode ToJson(object model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return JsonSerializer.SerializeToNode(model, model.GetType(), _json);
    }

    /// <summary>
    /// Turns JSON into plain dictionaries (names compared without case), lists and values.
    /// </summary>
    /// <param name="node">The JSON.</param>
    /// <returns>The plain value.</returns>
    public static object ToPlain(JsonNode node)
        => node switch
        {
            JsonObject obj => obj.ToDictionary(pair => pair.Key, pair => ToPlain(pair.Value), StringComparer.OrdinalIgnoreCase),
            JsonArray array => array.Select(ToPlain).ToList(),
            JsonValue value when value.TryGetValue<bool>(out var flag) => flag,
            JsonValue value when value.TryGetValue<decimal>(out var number) => number,
            JsonValue value => value.ToString(),
            _ => null,
        };
}
