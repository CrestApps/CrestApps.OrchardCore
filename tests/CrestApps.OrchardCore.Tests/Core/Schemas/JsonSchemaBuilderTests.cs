using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class JsonSchemaBuilderTests
{
    [Fact]
    public void Build_EmptyBuilder_ProducesEmptyObject()
    {
        var schema = new JsonSchemaBuilder().Build();
        var json = JsonSerializer.Serialize(schema);

        Assert.Equal("{}", json);
    }

    [Fact]
    public void Type_SetsCorrectKeyword()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.String).Build();
        var json = JsonSerializer.Serialize(schema);

        Assert.Contains("\"type\":\"string\"", json);
    }

    [Theory]
    [InlineData(SchemaValueType.Object, "object")]
    [InlineData(SchemaValueType.Array, "array")]
    [InlineData(SchemaValueType.Boolean, "boolean")]
    [InlineData(SchemaValueType.Number, "number")]
    [InlineData(SchemaValueType.Integer, "integer")]
    public void Type_MapsAllValues(SchemaValueType input, string expected)
    {
        var schema = new JsonSchemaBuilder().Type(input).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal(expected, node["type"]?.GetValue<string>());
    }

    [Fact]
    public void Description_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Description("test desc").Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal("test desc", node["description"]?.GetValue<string>());
    }

    [Fact]
    public void Const_String_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Const("fixed").Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal("fixed", node["const"]?.GetValue<string>());
    }

    [Fact]
    public void Const_Bool_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Const(true).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.True(node["const"]?.GetValue<bool>());
    }

    [Fact]
    public void Enum_Params_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Enum("a", "b", "c").Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        var arr = node["enum"]?.AsArray();
        Assert.NotNull(arr);
        Assert.Equal(3, arr.Count);
        Assert.Equal("b", arr[1]?.GetValue<string>());
    }

    [Fact]
    public void Enum_Enumerable_ReplacesValues()
    {
        var schema = new JsonSchemaBuilder()
            .Enum("x", "y", "z")
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        var arr = node["enum"]?.AsArray();

        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void Properties_EmitsNestedObjects()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("age", new JsonSchemaBuilder().Type(SchemaValueType.Number)))
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        var props = node["properties"]?.AsObject();

        Assert.NotNull(props);
        Assert.True(props.ContainsKey("name"));
        Assert.True(props.ContainsKey("age"));
    }

    [Fact]
    public void Required_EmitsStringArray()
    {
        var schema = new JsonSchemaBuilder()
            .Required("a", "b")
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        var arr = node["required"]?.AsArray();

        Assert.Equal(2, arr.Count);
        Assert.Equal("a", arr[0]?.GetValue<string>());
    }

    [Fact]
    public void AdditionalProperties_EmitsBool()
    {
        var schema = new JsonSchemaBuilder().AdditionalProperties(false).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.False(node["additionalProperties"]?.GetValue<bool>());
    }

    [Fact]
    public void MinProperties_EmitsInt()
    {
        var schema = new JsonSchemaBuilder().MinProperties(3).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal(3, node["minProperties"]?.GetValue<int>());
    }

    [Fact]
    public void Items_And_MinItems_EmitCorrectly()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .MinItems(1)
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.NotNull(node["items"]?.AsObject());
        Assert.Equal(1, node["minItems"]?.GetValue<int>());
    }

    [Fact]
    public void AllOf_EmitsSchemaArray()
    {
        var s1 = new JsonSchemaBuilder().Type(SchemaValueType.Object).Build();
        var s2 = new JsonSchemaBuilder().Required("x").Build();

        var schema = new JsonSchemaBuilder()
            .AllOf([s1, s2])
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        Assert.Equal(2, node["allOf"]?.AsArray().Count);
    }

    [Fact]
    public void AnyOf_FromBuilders_EmitsSchemaArray()
    {
        var schema = new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder().Type(SchemaValueType.String),
                new JsonSchemaBuilder().Type(SchemaValueType.Number))
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        Assert.Equal(2, node["anyOf"]?.AsArray().Count);
    }

    [Fact]
    public void OneOf_EmitsSchemaArray()
    {
        var s1 = new JsonSchemaBuilder().Const("a").Build();
        var s2 = new JsonSchemaBuilder().Const("b").Build();

        var schema = new JsonSchemaBuilder()
            .OneOf([s1, s2])
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));
        Assert.Equal(2, node["oneOf"]?.AsArray().Count);
    }

    [Fact]
    public void IfThenElse_EmitsAllThreeKeywords()
    {
        var schema = new JsonSchemaBuilder()
            .If(new JsonSchemaBuilder().Required("x"))
            .Then(new JsonSchemaBuilder().Required("y"))
            .Else(new JsonSchemaBuilder().Required("z"))
            .Build();

        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.NotNull(node["if"]);
        Assert.NotNull(node["then"]);
        Assert.NotNull(node["else"]);
    }

    [Fact]
    public void Pattern_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Pattern("^abc").Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal("^abc", node["pattern"]?.GetValue<string>());
    }

    [Fact]
    public void Default_String_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Default("hello").Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal("hello", node["default"]?.GetValue<string>());
    }

    [Fact]
    public void Default_Int_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Default(42).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.Equal(42, node["default"]?.GetValue<int>());
    }

    [Fact]
    public void Default_Bool_SetsKeyword()
    {
        var schema = new JsonSchemaBuilder().Default(true).Build();
        var node = JsonSerializer.Deserialize<JsonObject>(JsonSerializer.Serialize(schema));

        Assert.True(node["default"]?.GetValue<bool>());
    }

    [Fact]
    public void ImplicitConversion_ProducesEquivalentSchema()
    {
        JsonSchema schema = new JsonSchemaBuilder().Type(SchemaValueType.String);
        var json = JsonSerializer.Serialize(schema);

        Assert.Contains("\"type\":\"string\"", json);
    }

    [Fact]
    public void Serialization_RoundTrip_ProducesSameJson()
    {
        var original = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .Required("name")
            .Build();

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<JsonSchema>(json);
        var json2 = JsonSerializer.Serialize(deserialized);

        Assert.Equal(json, json2);
    }
}
