using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb.Protocol;

namespace CrestApps.OrchardCore.Tests.NLWeb;

public sealed class NLWebRequestReaderTests
{
    [Fact]
    public void TryRead_ReadsAVersion054Request()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "query": {
                "text": "something protein rich that has cinnamon in it",
                "site": "recipes",
                "itemType": "Recipe",
                "location": "Idaho",
                "price": "less than $20"
              },
              "context": {
                "@type": "ConversationalContext",
                "prev": ["breakfast muffins", "high fiber snacks"],
                "text": "Planning a party",
                "memory": "vegetarian"
              },
              "prefer": {
                "streaming": false,
                "response_format": "conversational_search",
                "mode": "Summarize",
                "accept-language": "en",
                "user-agent": "RecipeBot/2.0"
              },
              "meta": {
                "version": "0.54",
                "session_context": { "conversation_id": "conv_98765", "state_token": "xyz" }
              }
            }
            """).AsObject();

        // Act
        var read = NLWebRequestReader.TryRead(body, out var request, out var error);

        // Assert
        Assert.True(read, error);
        Assert.Equal("something protein rich that has cinnamon in it", request.Query.Text);
        Assert.Equal("recipes", request.Query.Site);
        Assert.Equal("Recipe", request.Query.ItemType);
        Assert.Equal("Idaho", request.Query.Filters["location"]?.GetValue<string>());
        Assert.Equal("less than $20", request.Query.Filters["price"]?.GetValue<string>());
        Assert.Equal(["breakfast muffins", "high fiber snacks"], request.Context.Previous);
        Assert.Equal(["vegetarian"], request.Context.Memory);
        Assert.Equal("Planning a party", request.Context.Text);
        Assert.False(request.Prefer.Streaming);
        Assert.Equal("summarize", request.Prefer.Mode);
        Assert.Equal("conversational_search", request.Prefer.ResponseFormat);
        Assert.Equal("en", request.Prefer.Language);
        Assert.Equal("0.54", request.Meta.Version);
        Assert.Equal("conv_98765", request.Meta.ConversationId);
        Assert.Equal("xyz", request.Meta.SessionContext["state_token"]?.GetValue<string>());
        Assert.False(request.IsLegacy);
    }

    [Fact]
    public void TryRead_ReadsTheReturnResponseSectionOfVersion05()
    {
        // Arrange
        var body = JsonNode.Parse("""
            {
              "query": { "text": "pasta", "site": ["first", "second"] },
              "return_response": { "streaming": "true", "mode": "list", "lang": "fr" },
              "meta": { "api_version": "0.5", "conversation_id": "c1" }
            }
            """).AsObject();

        // Act
        var read = NLWebRequestReader.TryRead(body, out var request, out _);

        // Assert
        Assert.True(read);
        Assert.Equal("first", request.Query.Site);
        Assert.True(request.Prefer.Streaming);
        Assert.Equal("fr", request.Prefer.Language);
        Assert.Equal("0.5", request.Meta.Version);
        Assert.Equal("c1", request.Meta.ConversationId);
    }

    [Fact]
    public void TryRead_ReadsTheFlatBodyOfTheOriginalProtocolAndAStringQuery()
    {
        // Arrange
        var body = JsonNode.Parse("""
            { "query": "spicy snacks", "prev": "chips, salsa", "mode": "generate", "streaming": 0, "num_results": 5 }
            """).AsObject();

        // Act
        var read = NLWebRequestReader.TryRead(body, out var request, out _);

        // Assert
        Assert.True(read);
        Assert.Equal("spicy snacks", request.Query.Text);
        Assert.Equal(["chips", "salsa"], request.Context.Previous);
        Assert.Equal("generate", request.Prefer.Mode);
        Assert.False(request.Prefer.Streaming);
        Assert.Equal(5, request.Prefer.MaxResults);
    }

    [Theory]
    [InlineData("""{ "query": { "site": "x" } }""")]
    [InlineData("""{ "query": "   " }""")]
    [InlineData("""{ }""")]
    public void TryRead_WhenTheQueryTextIsMissing_Fails(string json)
    {
        // Act
        var read = NLWebRequestReader.TryRead(JsonNode.Parse(json).AsObject(), out var request, out var error);

        // Assert
        Assert.False(read);
        Assert.Null(request);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryRead_WhenTheQueryIsTooLong_Fails()
    {
        // Arrange
        var body = new JsonObject
        {
            ["query"] = new string('a', NLWebRequestReader.MaxQueryLength + 1),
        };

        // Act & Assert
        Assert.False(NLWebRequestReader.TryRead(body, out _, out _));
    }

    [Fact]
    public void TryRead_KeepsOnlyTheMostRecentPreviousQueries()
    {
        // Arrange
        var previous = new JsonArray();

        for (var i = 0; i < NLWebRequestReader.MaxPreviousQueries + 5; i++)
        {
            previous.Add($"q{i}");
        }

        var body = new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["text"] = "latest",
            },
            ["context"] = new JsonObject
            {
                ["prev"] = previous,
            },
        };

        // Act
        NLWebRequestReader.TryRead(body, out var request, out _);

        // Assert
        Assert.Equal(NLWebRequestReader.MaxPreviousQueries, request.Context.Previous.Count);
        Assert.Equal("q5", request.Context.Previous[0]);
    }

    [Fact]
    public void TryReadQueryString_ReadsAnOriginalProtocolRequest()
    {
        // Arrange
        var values = new Dictionary<string, string>
        {
            ["query"] = "vegan dessert",
            ["site"] = "recipes",
            ["prev"] = "cake,pie",
            ["mode"] = "summarize",
            ["streaming"] = "false",
            ["query_id"] = "q-1",
        };

        // Act
        var read = NLWebRequestReader.TryReadQueryString(values, out var request, out _);

        // Assert
        Assert.True(read);
        Assert.True(request.IsLegacy);
        Assert.Equal("vegan dessert", request.Query.Text);
        Assert.Equal("recipes", request.Query.Site);
        Assert.Equal(["cake", "pie"], request.Context.Previous);
        Assert.Equal("summarize", request.Prefer.Mode);
        Assert.False(request.Prefer.Streaming);
        Assert.Equal("q-1", request.Meta.QueryId);
    }
}
