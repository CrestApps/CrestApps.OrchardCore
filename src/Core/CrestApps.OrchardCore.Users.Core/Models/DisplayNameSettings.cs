using System.Text.Json.Serialization;
using CrestApps.Support.Json;

namespace CrestApps.OrchardCore.Users.Models;

public sealed class DisplayNameSettings
{
    public DisplayNameType Type { get; set; }

    public string Template { get; set; }

    public DisplayNamePropertyType DisplayName { get; set; }

    public DisplayNamePropertyType FirstName { get; set; }

    public DisplayNamePropertyType LastName { get; set; }

    public DisplayNamePropertyType MiddleName { get; set; }
}

[JsonConverter(typeof(BidirectionalJsonStringEnumConverterFactory))]
public enum DisplayNameType
{
    Username = 0,
    FirstThenLast = 1,
    LastThenFirst = 2,
    DisplayName = 3,
    Other = 4,
}

[JsonConverter(typeof(BidirectionalJsonStringEnumConverterFactory))]
public enum DisplayNamePropertyType
{
    None = 0,
    Optional = 1,
    Required = 2,
};
