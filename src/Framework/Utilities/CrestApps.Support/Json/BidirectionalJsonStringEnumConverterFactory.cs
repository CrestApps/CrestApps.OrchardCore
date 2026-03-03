using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.Support.Json;

public class BidirectionalJsonStringEnumConverterFactory : JsonConverterFactory
{
    private readonly JsonStringEnumConverter _converter;

    public BidirectionalJsonStringEnumConverterFactory()
        : this(null, allowIntegerValues: true)
    { }

    public BidirectionalJsonStringEnumConverterFactory(JsonNamingPolicy namingPolicy, bool allowIntegerValues)
    {
        _converter = new JsonStringEnumConverter(namingPolicy, allowIntegerValues);
    }

    public override bool CanConvert(Type typeToConvert)
        => _converter.CanConvert(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => _converter.CreateConverter(typeToConvert, options);
}

