using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

public sealed class SurveyPipingRendererTests
{
    [Fact]
    public async Task Render_ReplacesNamesAndEncodes_AndUnknownNamesRenderEmpty()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("name"), RuntimeSurveys.Choice("color", "red", "blue")));
        var response = new SurveyResponse { Score = 7.5m };

        response.Answers["name"] = new SurveyAnswer { Values = ["<b>Ann</b>"] };
        response.Answers["color"] = new SurveyAnswer { Values = ["blue"] };
        response.Variables["city"] = "Paris";

        // Act
        var text = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyPipingRenderer>().Render("Hi {name}, you like {color} in {var:city} ({score}) {nope}", definition, response)));

        // Assert
        Assert.Equal("Hi &lt;b&gt;Ann&lt;/b&gt;, you like BLUE in Paris (7.5) ", text);
    }

    [Fact]
    public async Task Render_HiddenQuestion_RendersEmpty()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("name")));
        var response = new SurveyResponse();

        response.Answers["name"] = new SurveyAnswer { Values = ["Ann"] };

        // Act
        var text = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyPipingRenderer>().Render("[{name}]", definition, response, new HashSet<string> { "name" })));

        // Assert
        Assert.Equal("[]", text);
    }
}
