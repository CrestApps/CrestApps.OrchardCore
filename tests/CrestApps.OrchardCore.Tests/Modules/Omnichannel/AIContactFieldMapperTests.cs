using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class AIContactFieldMapperTests
{
    [Fact]
    public void Overwrite_ReplacesATextField()
    {
        var content = Content("""{"Contact":{"Company":{"Text":"Old Co"}}}""");

        Assert.True(AIContactFieldMapper.Apply(content, "Contact.Company", "TextField", "New Co", AIContactFieldMappingMode.Overwrite));
        Assert.Equal("New Co", content["Contact"]["Company"]["Text"].GetValue<string>());
    }

    [Fact]
    public void FillIfEmpty_LeavesAFieldThatHoldsSomething()
    {
        var content = Content("""{"Contact":{"Company":{"Text":"Old Co"},"Title":{"Text":""}}}""");

        Assert.False(AIContactFieldMapper.Apply(content, "Contact.Company", "TextField", "New Co", AIContactFieldMappingMode.FillIfEmpty));
        Assert.True(AIContactFieldMapper.Apply(content, "Contact.Title", "TextField", "Buyer", AIContactFieldMappingMode.FillIfEmpty));
        Assert.Equal("Old Co", content["Contact"]["Company"]["Text"].GetValue<string>());
        Assert.Equal("Buyer", content["Contact"]["Title"]["Text"].GetValue<string>());
    }

    [Fact]
    public void Append_AddsOnlyWhatIsNew()
    {
        var content = Content("""{"Contact":{"Interests":{"Text":"Solar"}}}""");

        Assert.True(AIContactFieldMapper.Apply(content, "Contact.Interests", "TextField", "Batteries", AIContactFieldMappingMode.Append));
        Assert.False(AIContactFieldMapper.Apply(content, "Contact.Interests", "TextField", "solar", AIContactFieldMappingMode.Append));
        Assert.Equal("Solar; Batteries", content["Contact"]["Interests"]["Text"].GetValue<string>());
    }

    [Fact]
    public void TypedFields_TakeTypedValues_AndRefuseOnesThatDoNotParse()
    {
        var content = Content("{}");

        Assert.True(AIContactFieldMapper.Apply(content, "Contact.Budget", "NumericField", "1250.50", AIContactFieldMappingMode.Overwrite));
        Assert.True(AIContactFieldMapper.Apply(content, "Contact.OptIn", "BooleanField", "yes", AIContactFieldMappingMode.Overwrite));
        Assert.True(AIContactFieldMapper.Apply(content, "Contact.Callback", "DateField", "2026-11-03", AIContactFieldMappingMode.Overwrite));
        Assert.False(AIContactFieldMapper.Apply(content, "Contact.Budget", "NumericField", "a lot", AIContactFieldMappingMode.Overwrite));
        Assert.False(AIContactFieldMapper.Apply(content, "NoPartName", "TextField", "x", AIContactFieldMappingMode.Overwrite));

        Assert.Equal(1250.50m, content["Contact"]["Budget"]["Value"].GetValue<decimal>());
        Assert.True(content["Contact"]["OptIn"]["Value"].GetValue<bool>());
        Assert.Equal(new DateTime(2026, 11, 3), content["Contact"]["Callback"]["Value"].GetValue<DateTime>());
    }

    [Fact]
    public void TheSameValue_IsNotAChange()
    {
        var content = Content("""{"Contact":{"Company":{"Text":"Acme"}}}""");

        Assert.False(AIContactFieldMapper.Apply(content, "Contact.Company", "TextField", "Acme", AIContactFieldMappingMode.Overwrite));
    }

    private static JsonObject Content(string json)
        => JsonNode.Parse(json).AsObject();
}
