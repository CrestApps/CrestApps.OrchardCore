using CrestApps.OrchardCore.Surveys.Core.Logic;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Tests.Surveys.Runtime;

/// <summary>
/// The ending renders the author's Liquid first and pipes answers in afterwards, so a respondent's answer never runs as
/// Liquid.
/// </summary>
public sealed class LiquidSurveyEndingRendererTests
{
    private const string Injection = "[{{ 7 | plus: 6 }}] [{% if true %}EXECUTED{% endif %}] {var:secret}";

    [Fact]
    public async Task Render_PipedAnswerWithLiquid_ComesOutLiterally()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (definition, response) = Answered(Injection);
        var ending = new SurveyEnding { Id = "e1", Name = "thanks", Message = "Thanks! You said: {comment}" };

        // Act
        var html = await RenderAsync(harness, definition, response, ending);

        // Assert
        Assert.DoesNotContain("[13]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[EXECUTED]", html, StringComparison.Ordinal);
        Assert.Contains("7 | plus: 6", html, StringComparison.Ordinal);
        Assert.Contains("{% if true %}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-value", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AuthorLiquidAndPiping_BothWork_AndTheAnswerStaysData()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (definition, response) = Answered(Injection);
        var ending = new SurveyEnding
        {
            Id = "e1",
            Name = "thanks",
            Message = "<p>{% if Response.Number %}Number {{ Response.Number }}{% endif %}</p><p>Liquid: {{ Answers.comment }}</p><p>Piped: {comment}</p>",
        };

        // Act
        var html = await RenderAsync(harness, definition, response, ending);

        // Assert: the author's Liquid ran, and the answer, written by Liquid and by piping, is plain text.
        Assert.Contains("Number R0000001", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[13]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("[EXECUTED]", html, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-value", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(html, "7 | plus: 6"));
    }

    [Fact]
    public async Task Render_AnswerWithMarkup_IsEncodedAndSanitized()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (definition, response) = Answered("<img src=x onerror=alert(1)>");
        var ending = new SurveyEnding { Id = "e1", Name = "thanks", Message = "You said: {comment}" };

        // Act
        var html = await RenderAsync(harness, definition, response, ending);

        // Assert
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }

    // R11-L4: the ending runs restricted Liquid, as the invitation templates do. A filter of Orchard Core's full surface
    // (here markdownify) is not run, and the message shows without its Liquid, never as template code.
    [Fact]
    public async Task Render_AFilterOutsideTheRestrictedSet_IsNotRun()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync();
        var (definition, response) = Answered("hello");
        var ending = new SurveyEnding { Id = "e1", Name = "thanks", Message = "<p>Thanks {{ \"**bold**\" | markdownify }}</p>" };

        // Act
        var html = await RenderAsync(harness, definition, response, ending);

        // Assert
        Assert.DoesNotContain("<strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("markdownify", html, StringComparison.Ordinal);
        Assert.Contains("Thanks", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Renderer_DoesNotTakeOrchardCoresLiquidTemplateManager()
    {
        // Act: the full Liquid surface reads content by id, saved queries and the site settings.
        var parameters = typeof(LiquidSurveyEndingRenderer).GetConstructors().SelectMany(constructor => constructor.GetParameters());

        // Assert
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(global::OrchardCore.Liquid.ILiquidTemplateManager));
    }

    private static (SurveyDefinition Definition, SurveyResponse Response) Answered(string answer)
    {
        var definition = RuntimeSurveys.Definition(RuntimeSurveys.Page("p1", RuntimeSurveys.Text("comment")));
        var response = new SurveyResponse { Number = "R0000001" };

        definition.Variables.Add(new SurveyVariable { Name = "secret", Kind = VariableKind.Hidden });
        response.Answers["comment"] = new SurveyAnswer { Values = [answer] };
        response.Variables["secret"] = "hidden-value";

        return (definition, response);
    }

    private static Task<string> RenderAsync(RuntimeTestHarness harness, SurveyDefinition definition, SurveyResponse response, SurveyEnding ending)
        => harness.RunAsync(services =>
        {
            var renderer = new LiquidSurveyEndingRenderer(
                services.GetRequiredService<SurveyPipingRenderer>(),
                new HtmlSanitizerService(Options.Create(new HtmlSanitizerOptions())),
                services.GetRequiredService<ISurveyControlRegistry>(),
                Mock.Of<global::OrchardCore.Modules.ILocalClock>(clock => clock.GetLocalTimeZoneAsync() == Task.FromResult<global::OrchardCore.Modules.ITimeZone>(null)),
                NullLogger<LiquidSurveyEndingRenderer>.Instance);

            return renderer.RenderAsync(new Survey { Name = "s", Title = "Survey" }, definition, response, ending, showScore: true);
        });

    private static int CountOf(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
