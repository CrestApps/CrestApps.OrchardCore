using System.Text.Json.Nodes;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class JsonSchemaValidationTests
{
    // ── Type checking ───────────────────────────────────────

    [Fact]
    public void Evaluate_ObjectType_PassesForObject()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Object).Build();
        var instance = JsonNode.Parse("{}");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_ObjectType_FailsForArray()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Object).Build();
        var instance = JsonNode.Parse("[]");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_StringType_PassesForString()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.String).Build();
        var instance = JsonNode.Parse("\"hello\"");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_StringType_FailsForNumber()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.String).Build();
        var instance = JsonNode.Parse("42");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_BooleanType_PassesForTrue()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Build();
        var instance = JsonNode.Parse("true");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_BooleanType_PassesForFalse()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Build();
        var instance = JsonNode.Parse("false");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_NumberType_PassesForInt()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Number).Build();
        var instance = JsonNode.Parse("99");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_ArrayType_PassesForArray()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.Array).Build();
        var instance = JsonNode.Parse("[1,2,3]");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    // ── Const ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_Const_PassesForMatch()
    {
        var schema = new JsonSchemaBuilder().Const("hello").Build();
        var instance = JsonNode.Parse("\"hello\"");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_Const_FailsForMismatch()
    {
        var schema = new JsonSchemaBuilder().Const("hello").Build();
        var instance = JsonNode.Parse("\"world\"");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── Enum ────────────────────────────────────────────────

    [Fact]
    public void Evaluate_Enum_PassesForIncludedValue()
    {
        var schema = new JsonSchemaBuilder().Enum("a", "b", "c").Build();
        var instance = JsonNode.Parse("\"b\"");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_Enum_FailsForExcludedValue()
    {
        var schema = new JsonSchemaBuilder().Enum("a", "b").Build();
        var instance = JsonNode.Parse("\"z\"");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── Pattern ─────────────────────────────────────────────

    [Fact]
    public void Evaluate_Pattern_PassesForMatch()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Pattern("^[a-z]+$")
            .Build();

        var instance = JsonNode.Parse("\"abc\"");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_Pattern_FailsForMismatch()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Pattern("^[a-z]+$")
            .Build();

        var instance = JsonNode.Parse("\"ABC123\"");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── Required ────────────────────────────────────────────

    [Fact]
    public void Evaluate_Required_PassesWhenPresent()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Required("name")
            .Build();

        var instance = JsonNode.Parse("""{"name":"test"}""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_Required_FailsWhenMissing()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Required("name")
            .Build();

        var instance = JsonNode.Parse("""{"other":"value"}""");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── AdditionalProperties ────────────────────────────────

    [Fact]
    public void Evaluate_AdditionalPropertiesFalse_PassesWithOnlyDeclaredProps()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(false)
            .Build();

        var instance = JsonNode.Parse("""{"name":"test"}""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_AdditionalPropertiesFalse_FailsWithExtraProps()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(false)
            .Build();

        var instance = JsonNode.Parse("""{"name":"test","extra":1}""");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_AdditionalPropertiesTrue_AllowsExtraProps()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true)
            .Build();

        var instance = JsonNode.Parse("""{"name":"test","extra":1}""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    // ── MinProperties ───────────────────────────────────────

    [Fact]
    public void Evaluate_MinProperties_PassesWhenSufficient()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .MinProperties(2)
            .Build();

        var instance = JsonNode.Parse("""{"a":1,"b":2}""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_MinProperties_FailsWhenInsufficient()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .MinProperties(3)
            .Build();

        var instance = JsonNode.Parse("""{"a":1}""");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── Items ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_Items_PassesWhenAllElementsMatch()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Build();

        var instance = JsonNode.Parse("""["a","b"]""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_Items_FailsWhenElementMismatches()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
            .Build();

        var instance = JsonNode.Parse("""["a",42]""");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── MinItems ────────────────────────────────────────────

    [Fact]
    public void Evaluate_MinItems_FailsWhenTooFew()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .MinItems(2)
            .Build();

        var instance = JsonNode.Parse("[1]");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── Nested property validation ──────────────────────────

    [Fact]
    public void Evaluate_NestedProperties_ValidatesRecursively()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("address", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Required("city")))
            .Build();

        var valid = JsonNode.Parse("""{"address":{"city":"NY"}}""");
        var invalid = JsonNode.Parse("""{"address":{"zip":"10001"}}""");

        Assert.True(schema.Evaluate(valid).IsValid);
        Assert.False(schema.Evaluate(invalid).IsValid);
    }

    // ── AllOf ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_AllOf_PassesWhenAllMatch()
    {
        var s1 = new JsonSchemaBuilder().Required("a").Build();
        var s2 = new JsonSchemaBuilder().Required("b").Build();

        var schema = new JsonSchemaBuilder().AllOf([s1, s2]).Build();
        var instance = JsonNode.Parse("""{"a":1,"b":2}""");

        Assert.True(schema.Evaluate(instance).IsValid);
    }

    [Fact]
    public void Evaluate_AllOf_FailsWhenOneDoesNotMatch()
    {
        var s1 = new JsonSchemaBuilder().Required("a").Build();
        var s2 = new JsonSchemaBuilder().Required("b").Build();

        var schema = new JsonSchemaBuilder().AllOf([s1, s2]).Build();
        var instance = JsonNode.Parse("""{"a":1}""");

        Assert.False(schema.Evaluate(instance).IsValid);
    }

    // ── AnyOf ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_AnyOf_PassesWhenAtLeastOneMatches()
    {
        var schema = new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder().Type(SchemaValueType.String),
                new JsonSchemaBuilder().Type(SchemaValueType.Number))
            .Build();

        Assert.True(schema.Evaluate(JsonNode.Parse("\"hello\"")).IsValid);
        Assert.True(schema.Evaluate(JsonNode.Parse("42")).IsValid);
    }

    [Fact]
    public void Evaluate_AnyOf_FailsWhenNoneMatch()
    {
        var schema = new JsonSchemaBuilder()
            .AnyOf(
                new JsonSchemaBuilder().Type(SchemaValueType.String),
                new JsonSchemaBuilder().Type(SchemaValueType.Number))
            .Build();

        Assert.False(schema.Evaluate(JsonNode.Parse("true")).IsValid);
    }

    // ── OneOf ───────────────────────────────────────────────

    [Fact]
    public void Evaluate_OneOf_PassesWhenExactlyOneMatches()
    {
        var s1 = new JsonSchemaBuilder().Const("a").Build();
        var s2 = new JsonSchemaBuilder().Const("b").Build();

        var schema = new JsonSchemaBuilder().OneOf([s1, s2]).Build();

        Assert.True(schema.Evaluate(JsonNode.Parse("\"a\"")).IsValid);
    }

    [Fact]
    public void Evaluate_OneOf_FailsWhenNoneMatch()
    {
        var s1 = new JsonSchemaBuilder().Const("a").Build();
        var s2 = new JsonSchemaBuilder().Const("b").Build();

        var schema = new JsonSchemaBuilder().OneOf([s1, s2]).Build();

        Assert.False(schema.Evaluate(JsonNode.Parse("\"c\"")).IsValid);
    }

    // ── If/Then/Else ────────────────────────────────────────

    [Fact]
    public void Evaluate_IfThen_AppliesThenBranchWhenConditionMet()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .If(new JsonSchemaBuilder()
                .Properties(("role", new JsonSchemaBuilder().Const("admin")))
                .Required("role"))
            .Then(new JsonSchemaBuilder().Required("accessLevel"))
            .Build();

        var valid = JsonNode.Parse("""{"role":"admin","accessLevel":"full"}""");
        var invalid = JsonNode.Parse("""{"role":"admin"}""");
        var noMatch = JsonNode.Parse("""{"role":"user"}""");

        Assert.True(schema.Evaluate(valid).IsValid);
        Assert.False(schema.Evaluate(invalid).IsValid);
        Assert.True(schema.Evaluate(noMatch).IsValid); // condition not met, no constraint
    }

    [Fact]
    public void Evaluate_IfElse_AppliesElseBranchWhenConditionNotMet()
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .If(new JsonSchemaBuilder()
                .Properties(("isStart", new JsonSchemaBuilder().Const(true)))
                .Required("isStart"))
            .Then(new JsonSchemaBuilder()
                .Properties(("name", new JsonSchemaBuilder().Enum("EventA", "EventB"))))
            .Else(new JsonSchemaBuilder()
                .Properties(("name", new JsonSchemaBuilder().Enum("TaskX", "TaskY"))))
            .Build();

        var eventOk = JsonNode.Parse("""{"isStart":true,"name":"EventA"}""");
        var taskOk = JsonNode.Parse("""{"name":"TaskX"}""");
        var eventBad = JsonNode.Parse("""{"isStart":true,"name":"TaskX"}""");
        var taskBad = JsonNode.Parse("""{"name":"EventA"}""");

        Assert.True(schema.Evaluate(eventOk).IsValid);
        Assert.True(schema.Evaluate(taskOk).IsValid);
        Assert.False(schema.Evaluate(eventBad).IsValid);
        Assert.False(schema.Evaluate(taskBad).IsValid);
    }

    // ── Empty/null schema ───────────────────────────────────

    [Fact]
    public void Evaluate_EmptySchema_PassesAnything()
    {
        var schema = new JsonSchemaBuilder().Build();

        Assert.True(schema.Evaluate(JsonNode.Parse("42")).IsValid);
        Assert.True(schema.Evaluate(JsonNode.Parse("\"text\"")).IsValid);
        Assert.True(schema.Evaluate(JsonNode.Parse("{}")).IsValid);
    }

    // ── Complex realistic scenario ──────────────────────────

    [Fact]
    public void Evaluate_RecipeSchema_ValidatesCorrectly()
    {
        // Simulates the recipe validation used in ImportRecipeBaseTool
        var settingsStep = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("settings")))
            .Required("name")
            .MinProperties(2)
            .AdditionalProperties(true)
            .Build();

        var recipeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("steps", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().OneOf([settingsStep]))
                    .MinItems(1)))
            .Required("steps")
            .Build();

        var validRecipe = JsonNode.Parse("""
        {
            "steps": [
                { "name": "settings", "BaseUrl": "https://example.com" }
            ]
        }
        """);

        var missingSteps = JsonNode.Parse("""{}""");

        var wrongStepName = JsonNode.Parse("""
        {
            "steps": [
                { "name": "unknown" }
            ]
        }
        """);

        Assert.True(recipeSchema.Evaluate(validRecipe).IsValid);
        Assert.False(recipeSchema.Evaluate(missingSteps).IsValid);
        Assert.False(recipeSchema.Evaluate(wrongStepName).IsValid);
    }

    [Fact]
    public void Evaluate_WithEvaluationOptions_DoesNotThrow()
    {
        var schema = new JsonSchemaBuilder().Type(SchemaValueType.String).Build();
        var instance = JsonNode.Parse("\"ok\"");

        var result = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid);
    }
}
