using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Extensions;

public sealed class AIFunctionArgumentsExtensionsTests
{
    [Fact]
    public static void TryGetFirstString_WhenEmptyString_ReturnsFalse()
    {
        var arguments = new AIFunctionArguments()
        {
            { "test","" },
        };

        var result1 = arguments.TryGetFirstString("test", out var value1);
        var result2 = arguments.TryGetFirstString("test", false, out var value2);

        Assert.False(result1);
        Assert.Null(value1);

        Assert.False(result2);
        Assert.Null(value2);
    }

    [Fact]
    public static void TryGetFirstString_WhenEmptyStringAndNotSet_ReturnsTrue()
    {
        var actualValue = "";
        var arguments = new AIFunctionArguments()
        {
            { "test",actualValue },
        };

        var result = arguments.TryGetFirstString("test", true, out var value);

        Assert.True(result);
        Assert.Equal(actualValue, value);
    }

    [Fact]
    public static void TryGetFirstString_WhenValueExists_ReturnsTrueAndTheCorrectValue()
    {
        var actualValue = "good";

        var arguments = new AIFunctionArguments()
        {
            { "test",actualValue },
        };

        var result = arguments.TryGetFirstString("test", out var value);

        Assert.True(result);
        Assert.Equal(actualValue, value);
    }

    [Fact]
    public static void TryGetFirstString_WhenStringJsonElement_ReturnsTrueAndTheCorrectValue()
    {
        using var doc = JsonDocument.Parse("\"Mike\"");

        var arguments = new AIFunctionArguments()
        {
            { "name", doc.RootElement },
        };

        var result = arguments.TryGetFirstString("name", out var value);

        Assert.True(result);
        Assert.Equal("Mike", value);
    }


    [Fact]
    public static void TryGetFirst_WhenNumberJsonElement_ReturnsTrueAndTheCorrectValue()
    {
        using var doc = JsonDocument.Parse("10");

        var arguments = new AIFunctionArguments()
        {
            { "age", doc.RootElement },
        };

        var result = arguments.TryGetFirst<int>("age", out var value);

        Assert.True(result);
        Assert.Equal(10, value);
    }

    [Fact]
    public static void TryGetFirst_WhenObjectJsonElement_ReturnsTrueAndTheCorrectValue()
    {
        using var doc = JsonDocument.Parse("{\"name\":\"Jace\",\"age\":10}");

        var arguments = new AIFunctionArguments()
        {
            { "child", doc.RootElement },
        };

        var result = arguments.TryGetFirst<Person>("child", out var value);

        Assert.True(result);
        Assert.Equal("Jace", value.Name);
        Assert.Equal(10, value.Age);
    }

    [Fact]
    public static void TryGetFirst_WhenArrayJsonElement_ReturnsTrueAndTheCorrectValue()
    {
        using var doc = JsonDocument.Parse("[\"one\",\"two\",\"three\"]");

        var arguments = new AIFunctionArguments()
        {
            { "options", doc.RootElement },
        };

        var result = arguments.TryGetFirst<string[]>("options", out var value);

        Assert.True(result);
        Assert.Equal(3, value.Length);

        Assert.Equal("one", value[0]);
        Assert.Equal("two", value[1]);
        Assert.Equal("three", value[2]);
    }

    private sealed class Person
    {
        public string Name { get; set; }

        public int Age { get; set; }
    }
}
