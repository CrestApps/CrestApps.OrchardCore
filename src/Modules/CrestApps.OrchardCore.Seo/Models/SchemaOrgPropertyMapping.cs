using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Seo.Models;

/// <summary>
/// One schema.org property of a content type and where its value comes from.
/// </summary>
public sealed class SchemaOrgPropertyMapping
{
    /// <summary>
    /// Gets or sets the property name, such as <c>headline</c>. A dotted name, such as <c>offers.price</c>, sets a
    /// property of a nested object.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where the value comes from.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<SchemaOrgValueSource>))]
    public SchemaOrgValueSource Source { get; set; }

    /// <summary>
    /// Gets or sets what <see cref="Source"/> needs: the key of a field or of a value of the content item, such as
    /// <c>Field:BlogPost.Image</c> or <c>Item:DisplayText</c>; the value itself; or the Liquid template.
    /// </summary>
    public string Value { get; set; }
}

/// <summary>
/// Where the value of a schema.org property comes from.
/// </summary>
public enum SchemaOrgValueSource
{
    /// <summary>
    /// A field or a value of the content item, such as its title, its author or one of its fields.
    /// </summary>
    Content,

    /// <summary>
    /// The same value for every content item of the type.
    /// </summary>
    Fixed,

    /// <summary>
    /// A value editors enter on each content item.
    /// </summary>
    Editor,

    /// <summary>
    /// A Liquid template, for values built from several fields.
    /// </summary>
    Liquid,
}
