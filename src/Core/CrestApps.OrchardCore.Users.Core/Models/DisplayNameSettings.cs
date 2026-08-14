using System.Text.Json.Serialization;
using CrestApps.Core.Support.Json;

namespace CrestApps.OrchardCore.Users.Core.Models;

/// <summary>
/// Represents the display name settings.
/// </summary>
public sealed class DisplayNameSettings
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public DisplayNameType Type { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public string Template { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public DisplayNamePropertyType DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public DisplayNamePropertyType FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public DisplayNamePropertyType LastName { get; set; }

    /// <summary>
    /// Gets or sets the middle name.
    /// </summary>
    public DisplayNamePropertyType MiddleName { get; set; }
}

/// <summary>
/// Specifies the display name type options.
/// </summary>
[JsonConverter(typeof(BidirectionalJsonStringEnumConverterFactory))]
public enum DisplayNameType
{
    Username = 0,
    FirstThenLast = 1,
    LastThenFirst = 2,
    DisplayName = 3,
    Other = 4,
}

/// <summary>
/// Specifies the display name property type options.
/// </summary>
[JsonConverter(typeof(BidirectionalJsonStringEnumConverterFactory))]
public enum DisplayNamePropertyType
{
    None = 0,
    Optional = 1,
    Required = 2,
};
