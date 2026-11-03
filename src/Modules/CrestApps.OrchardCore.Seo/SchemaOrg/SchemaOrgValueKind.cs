namespace CrestApps.OrchardCore.Seo.SchemaOrg;

/// <summary>
/// The kind of value a schema.org property holds. It decides how a mapped value is written, such as a date in ISO 8601
/// or a name wrapped in a <c>Person</c>, which value sources the editor suggests, and which input editors fill in.
/// </summary>
public enum SchemaOrgValueKind
{
    /// <summary>
    /// Short text, such as a name or a headline.
    /// </summary>
    Text,

    /// <summary>
    /// Long text, such as a description or an article body. HTML is turned into plain text.
    /// </summary>
    LongText,

    /// <summary>
    /// A list of texts, such as keywords or recipe ingredients.
    /// </summary>
    TextList,

    /// <summary>
    /// An absolute URL.
    /// </summary>
    Url,

    /// <summary>
    /// The absolute URL of an image.
    /// </summary>
    Image,

    /// <summary>
    /// A date, written as <c>yyyy-MM-dd</c>.
    /// </summary>
    Date,

    /// <summary>
    /// A date and time, written in ISO 8601.
    /// </summary>
    DateTime,

    /// <summary>
    /// An ISO 8601 duration, such as <c>PT30M</c>.
    /// </summary>
    Duration,

    /// <summary>
    /// A number.
    /// </summary>
    Number,

    /// <summary>
    /// <see langword="true"/> or <see langword="false"/>.
    /// </summary>
    Boolean,

    /// <summary>
    /// One of a fixed list of schema.org values, such as <c>https://schema.org/InStock</c>.
    /// </summary>
    Enumeration,

    /// <summary>
    /// A schema.org object, such as a <c>Person</c> or a <c>Place</c>. Text becomes the object's <c>name</c>.
    /// </summary>
    Object,

    /// <summary>
    /// JSON written as it is, such as the questions of an FAQ page built with Liquid.
    /// </summary>
    Json,
}
