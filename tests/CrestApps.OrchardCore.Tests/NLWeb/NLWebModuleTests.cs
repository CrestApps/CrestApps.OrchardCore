using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.NLWeb;
using CrestApps.OrchardCore.NLWeb.AI;
using CrestApps.OrchardCore.NLWeb.Endpoints;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.NLWeb;

public sealed class NLWebModuleTests
{
    [Theory]
    [InlineData("What pies have cinnamon?", "What pies have cinnamon")]
    [InlineData("title:\"apple\" AND (pie OR tart)", "title apple AND pie OR tart")]
    [InlineData("C# & .NET 10 at info@example.com", "C .NET 10 at info@example.com")]
    [InlineData("???", "")]
    public void ToSearchTerms_RemovesTheCharactersQueryParsersReadAsOperators(string question, string expected)
    {
        Assert.Equal(expected, IndexSearchNLWebRetriever.ToSearchTerms(question));
    }

    [Fact]
    public void Apply_ClampsTheResultsAndFallsBackToListForAModeTheSiteDoesNotAllow()
    {
        // Arrange
        var request = new NLWebAskRequest();
        request.Query.Text = "pies";
        request.Query.Site = "BLOG";
        request.Prefer.Mode = NLWebProtocol.Modes.Generate;
        request.Prefer.MaxResults = 500;

        var context = new NLWebAskContext(request, null);
        var settings = new NLWebSettings
        {
            AllowGenerate = false,
            IndexProfileNames = ["Main", "Blog"],
        };

        // Act
        NLWebSettingsQueryProcessor.Apply(context, settings);

        // Assert
        Assert.Equal(NLWebConstants.MaxResultsLimit, context.MaxResults);
        Assert.Equal(NLWebProtocol.Modes.List, context.Mode);
        Assert.Equal("Blog", context.Properties[NLWebSettingsQueryProcessor.IndexProfileNameKey]);
    }

    [Fact]
    public void Apply_WhenTheSiteIsUnknown_UsesTheFirstIndexAndTheDefaultMode()
    {
        // Arrange
        var request = new NLWebAskRequest();
        request.Query.Text = "pies";
        request.Query.Site = "example.com";

        var context = new NLWebAskContext(request, null);
        var settings = new NLWebSettings
        {
            DefaultMode = NLWebProtocol.Modes.Summarize,
            IndexProfileNames = ["Main", "Blog"],
        };

        // Act
        NLWebSettingsQueryProcessor.Apply(context, settings);

        // Assert
        Assert.Equal("Main", context.Properties[NLWebSettingsQueryProcessor.IndexProfileNameKey]);
        Assert.Equal(NLWebProtocol.Modes.Summarize, context.Mode);
        Assert.Equal(settings.MaxResults, context.MaxResults);
    }

    [Fact]
    public void FilterByItemType_KeepsTheTypeAskedForAndFallsBackWhenNoneMatches()
    {
        // Arrange
        var results = new List<NLWebResult>
        {
            CreateResult("a", "Recipe"),
            CreateResult("b", "Article"),
        };

        // Act
        var recipes = ContentItemNLWebResultResolver.FilterByItemType(results, "recipe");
        var events = ContentItemNLWebResultResolver.FilterByItemType(results, "Event");

        // Assert
        Assert.Equal("a", Assert.Single(recipes).Id);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void RankerApply_OrdersByScoreDropsLowScoresAndKeepsTheReasons()
    {
        // Arrange
        var results = new List<NLWebResult>
        {
            CreateResult("a", "Recipe"),
            CreateResult("b", "Recipe"),
            CreateResult("c", "Recipe"),
        };

        var scores = new[]
        {
            new NLWebRankedItem { Index = 0, Score = 40, Reason = "Partly." },
            new NLWebRankedItem { Index = 1, Score = 90, Reason = "Exactly." },
            new NLWebRankedItem { Index = 2, Score = 5 },
            new NLWebRankedItem { Index = 9, Score = 100 },
        };

        // Act
        var ranked = AIResultRanker.Apply(results, scores, 10);

        // Assert
        Assert.Equal(["b", "a"], ranked.Select(result => result.Id));
        Assert.Equal("Exactly.", ranked[0].Relevance);
        Assert.Equal(0.9, ranked[0].Score);
    }

    [Fact]
    public void RankerApply_WhenEveryResultScoresLow_KeepsTheBestOnes()
    {
        // Arrange
        var results = new List<NLWebResult>
        {
            CreateResult("a", "Recipe"),
            CreateResult("b", "Recipe"),
        };

        // Act
        var ranked = AIResultRanker.Apply(results, [new NLWebRankedItem { Index = 1, Score = 10 }, new NLWebRankedItem { Index = 0, Score = 1 }], 1);

        // Assert
        Assert.Equal("b", Assert.Single(ranked).Id);
    }

    [Theory]
    [InlineData(null, "text/event-stream", true)]
    [InlineData(null, "application/json", false)]
    [InlineData(null, null, false)]
    [InlineData(false, "text/event-stream", false)]
    [InlineData(true, "application/json", true)]
    public void WantsStream_HonorsThePreferenceThenTheAcceptHeader(bool? streaming, string accept, bool expected)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        if (accept is not null)
        {
            httpContext.Request.Headers.Accept = accept;
        }

        var request = new NLWebAskRequest();
        request.Prefer.Streaming = streaming;

        // Act & Assert
        Assert.Equal(expected, NLWebEndpoints.WantsStream(httpContext.Request, request));
    }

    [Fact]
    public void AskToolArguments_BecomeTheBodyOfAnNLWebRequest()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["query"] = JsonSerializer.Deserialize<JsonElement>("""{"text":"vegan pies","itemType":"Recipe"}"""),
            ["prefer"] = new JsonObject
            {
                ["mode"] = "summarize",
            },
        };

        // Act
        var body = NLWebAskTool.ToJsonObject(arguments);

        // Assert
        Assert.Equal("vegan pies", body["query"]?["text"]?.GetValue<string>());
        Assert.Equal("Recipe", body["query"]?["itemType"]?.GetValue<string>());
        Assert.Equal("summarize", body["prefer"]?["mode"]?.GetValue<string>());
    }

    private static NLWebResult CreateResult(string id, string type)
        => new()
        {
            Id = id,
            Item = new JsonObject
            {
                ["@type"] = type,
            },
        };
}
