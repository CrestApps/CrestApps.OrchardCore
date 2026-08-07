using System.Text.Json.Nodes;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

/// <summary>
/// Provides reusable JSON schema fragments for describing sitemap source properties.
/// </summary>
public static class SitemapSchemaBuilders
{
    /// <summary>
    /// The sitemap change frequency values reported to search engines.
    /// </summary>
    public static readonly string[] ChangeFrequencyValues =
    [
        "Daily",
        "Hourly",
        "Weekly",
        "Monthly",
        "Yearly",
        "Always",
        "Never",
    ];

    /// <summary>
    /// Creates a schema for the <c>ChangeFrequency</c> enum property shared by content based sources.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder ChangeFrequency(string description)
        => EnumValue(description, ChangeFrequencyValues);

    /// <summary>
    /// Creates a schema for the <c>Priority</c> property shared by content based sources.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    public static JsonSchemaBuilder Priority(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Integer)
            .Minimum(0)
            .Maximum(10)
            .Description(description);

    /// <summary>
    /// Creates a schema for a .NET enum property that Orchard Core may serialize either as its member name
    /// or as its underlying ordinal value.
    /// </summary>
    /// <param name="description">The description shown for the property.</param>
    /// <param name="names">The enum member names, in declaration order.</param>
    public static JsonSchemaBuilder EnumValue(string description, params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        return new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(names),
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Enum(Enumerable.Range(0, names.Length).Select(value => (JsonNode)value)))
            .Description($"{description} Accepted values: {string.Join(", ", names)}.");
    }
}
