using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Protocol;

namespace CrestApps.OrchardCore.Tests.NLWeb;

public sealed class NLWebResponseBuilderTests
{
    [Fact]
    public void Build_WritesAnAnswerWithItsResultsSummaryAndMetadata()
    {
        // Arrange
        var builder = new NLWebResponseBuilder();

        // Act
        builder.Add(new NLWebStartedEvent("conv-1", "q-1"));
        builder.Add(new NLWebResultEvent(0, CreateResult("Pie", "https://example.com/pie")));
        builder.Add(new NLWebTextEvent("Two pies were found.", NLWebProtocol.Modes.Summarize));
        builder.Add(new NLWebCompletedEvent(1, 42, new JsonObject { ["conversation_id"] = "conv-1" }));

        var response = builder.Build();

        // Assert
        var meta = response["_meta"].AsObject();
        Assert.Equal("answer", meta["response_type"]?.GetValue<string>());
        Assert.Equal(NLWebProtocol.Version, meta["version"]?.GetValue<string>());
        Assert.Equal("conv-1", meta["conversation_id"]?.GetValue<string>());
        Assert.Equal("Two pies were found.", meta["summary"]?.GetValue<string>());
        Assert.Equal(1, meta["total_items"]?.GetValue<int>());
        Assert.Equal("conv-1", meta["session_context"]?["conversation_id"]?.GetValue<string>());

        var item = Assert.Single(response["results"].AsArray());
        Assert.Equal("Recipe", item["@type"]?.GetValue<string>());
        Assert.Equal("https://example.com/pie", item["url"]?.GetValue<string>());

        var content = Assert.Single(response["content"].AsArray());
        Assert.Equal("text", content["type"]?.GetValue<string>());
    }

    [Fact]
    public void Build_WhenTheQueryFailed_WritesAFailureWithItsError()
    {
        // Arrange
        var builder = new NLWebResponseBuilder();

        // Act
        builder.Add(new NLWebStartedEvent(null, "q-1"));
        builder.Add(new NLWebFailureEvent(NLWebProtocol.ErrorCodes.NotConfigured, "Not set up."));

        var response = builder.Build();

        // Assert
        Assert.Equal("failure", response["_meta"]?["response_type"]?.GetValue<string>());
        Assert.Equal(NLWebProtocol.ErrorCodes.NotConfigured, response["error"]?["code"]?.GetValue<string>());
        Assert.Null(response["results"]);
    }

    [Fact]
    public void Build_WhenMoreInformationIsNeeded_WritesTheElicitation()
    {
        // Arrange
        var builder = new NLWebResponseBuilder();
        var question = new NLWebQuestion
        {
            Id = "diet",
            Text = "Any dietary restriction?",
            Type = "single_select",
            Options = ["vegan", "none"],
        };

        // Act
        builder.Add(new NLWebElicitationEvent("Tell me more:", [question]));

        var response = builder.Build();

        // Assert
        Assert.Equal("elicitation", response["_meta"]?["response_type"]?.GetValue<string>());
        var asked = Assert.Single(response["elicitation"]?["questions"]?.AsArray());
        Assert.Equal("diet", asked["id"]?.GetValue<string>());
        Assert.Equal(2, asked["options"]?.AsArray().Count);
    }

    [Fact]
    public void LegacyBuild_WritesTheOriginalProtocolShape()
    {
        // Arrange
        var builder = new NLWebLegacyResponseBuilder("example.com");
        var result = CreateResult("Pie", "https://example.com/pie");
        result.Score = 0.8;
        result.Relevance = "A sweet pie.";

        // Act
        builder.Add(new NLWebStartedEvent(null, "q-9"));
        builder.Add(new NLWebResultEvent(0, result));

        var response = builder.Build();

        // Assert
        Assert.Equal("q-9", response["query_id"]?.GetValue<string>());
        var item = Assert.Single(response["results"].AsArray());
        Assert.Equal("https://example.com/pie", item["url"]?.GetValue<string>());
        Assert.Equal("Pie", item["name"]?.GetValue<string>());
        Assert.Equal("example.com", item["site"]?.GetValue<string>());
        Assert.Equal(0.8, item["score"]?.GetValue<double>());
        Assert.Equal("A sweet pie.", item["description"]?.GetValue<string>());
        Assert.Equal("Recipe", JsonNode.Parse(item["schema_object"].GetValue<string>())?["@type"]?.GetValue<string>());
    }

    [Fact]
    public void StreamFrame_FormatsAServerSentEventWithJsonOnOneLine()
    {
        // Arrange
        var frame = NLWebStreamFrame.Create(new NLWebResultEvent(3, CreateResult("Pie", "https://example.com/pie")));

        // Act
        var text = frame.Format();

        // Assert
        Assert.StartsWith("event: result\ndata: {", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n\n", text, StringComparison.Ordinal);
        Assert.Equal(3, text.Split('\n').Length - 1);

        var data = JsonNode.Parse(text.Split('\n')[1].Substring("data: ".Length));
        Assert.Equal(3, data["index"]?.GetValue<int>());
        Assert.Equal("Pie", data["item"]?["name"]?.GetValue<string>());
    }

    [Fact]
    public void StreamFrame_StartAndCompleteCarryTheMetadata()
    {
        // Act
        var start = NLWebStreamFrame.Create(new NLWebStartedEvent("conv-1", "q-1"));
        var complete = NLWebStreamFrame.Create(new NLWebCompletedEvent(2, 10, null));

        // Assert
        Assert.Equal(NLWebProtocol.StreamEvents.Start, start.Name);
        Assert.Equal("answer", start.Data["_meta"]?["response_type"]?.GetValue<string>());
        Assert.Equal(NLWebProtocol.StreamEvents.Complete, complete.Name);
        Assert.Equal(2, complete.Data["_meta"]?["total_items"]?.GetValue<int>());
        Assert.Equal(NLWebProtocol.StreamEvents.End, NLWebStreamFrame.End.Name);
    }

    private static NLWebResult CreateResult(string name, string url)
        => new()
        {
            Id = name,
            Name = name,
            Url = url,
            Item = new JsonObject
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Recipe",
                ["name"] = name,
                ["url"] = url,
            },
        };
}
