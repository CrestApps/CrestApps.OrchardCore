using CrestApps.AI.Prompting;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Services;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

public sealed class AITemplateBuilderTests
{
    [Fact]
    public void Build_EmptyBuilder_ReturnsEmptyString()
    {
        var builder = new AITemplateBuilder();
        Assert.Equal(string.Empty, builder.Build());
    }

    [Fact]
    public void Build_SingleString_ReturnsThatString()
    {
        var builder = new AITemplateBuilder();
        builder.Append("Hello");
        Assert.Equal("Hello", builder.Build());
    }

    [Fact]
    public void Build_MultipleStrings_JoinsWithDefaultSeparator()
    {
        var builder = new AITemplateBuilder();
        builder.Append("Hello");
        builder.Append("World");

        var result = builder.Build();
        Assert.Equal("Hello" + Environment.NewLine + Environment.NewLine + "World", result);
    }

    [Fact]
    public void Build_CustomSeparator_UsesIt()
    {
        var builder = new AITemplateBuilder();
        builder.WithSeparator(" | ");
        builder.Append("A");
        builder.Append("B");
        builder.Append("C");

        Assert.Equal("A | B | C", builder.Build());
    }

    [Fact]
    public void Build_SkipsNullAndEmptyStrings()
    {
        var builder = new AITemplateBuilder();
        builder.WithSeparator(", ");
        builder.Append("A");
        builder.Append((string)null);
        builder.Append("");
        builder.Append("B");

        Assert.Equal("A, B", builder.Build());
    }

    [Fact]
    public void Build_AITemplate_AppendsContent()
    {
        var builder = new AITemplateBuilder();
        builder.WithSeparator("\n");
        builder.Append(new AITemplate { Id = "t1", Content = "Template content" });

        Assert.Equal("Template content", builder.Build());
    }

    [Fact]
    public void Build_NullAITemplate_Skipped()
    {
        var builder = new AITemplateBuilder();
        builder.Append((AITemplate)null);
        Assert.Equal(string.Empty, builder.Build());
    }

    [Fact]
    public void Build_AITemplateWithEmptyContent_Skipped()
    {
        var builder = new AITemplateBuilder();
        builder.Append(new AITemplate { Id = "t1", Content = "" });
        Assert.Equal(string.Empty, builder.Build());
    }

    [Fact]
    public void Build_MixedStringsAndTemplates()
    {
        var builder = new AITemplateBuilder();
        builder.WithSeparator("\n");
        builder.Append("Start");
        builder.Append(new AITemplate { Id = "t1", Content = "Middle" });
        builder.Append("End");

        Assert.Equal("Start\nMiddle\nEnd", builder.Build());
    }

    [Fact]
    public void Build_ThrowsWhenTemplateIdSegmentPresent()
    {
        var builder = new AITemplateBuilder();
        builder.AppendTemplate("some-template");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task BuildAsync_ResolvesTemplateIds()
    {
        var service = new FakeAITemplateService(new Dictionary<string, string>
        {
            ["greeting"] = "Hello from template!",
        });

        var builder = new AITemplateBuilder();
        builder.WithSeparator("\n");
        builder.Append("Before");
        builder.AppendTemplate("greeting");
        builder.Append("After");

        var result = await builder.BuildAsync(service);
        Assert.Equal("Before\nHello from template!\nAfter", result);
    }

    [Fact]
    public async Task BuildAsync_SkipsUnresolvedTemplates()
    {
        var service = new FakeAITemplateService([]);

        var builder = new AITemplateBuilder();
        builder.WithSeparator("\n");
        builder.Append("Before");
        builder.AppendTemplate("nonexistent");
        builder.Append("After");

        var result = await builder.BuildAsync(service);
        Assert.Equal("Before\nAfter", result);
    }

    [Fact]
    public async Task BuildAsync_PassesArguments()
    {
        var service = new FakeAITemplateService(new Dictionary<string, string>
        {
            ["t1"] = "Rendered with args",
        });

        var args = new Dictionary<string, object> { ["key"] = "value" };

        var builder = new AITemplateBuilder();
        builder.AppendTemplate("t1", args);

        var result = await builder.BuildAsync(service);
        Assert.Equal("Rendered with args", result);
    }

    [Fact]
    public async Task BuildAsync_ThrowsOnNullService()
    {
        var builder = new AITemplateBuilder();
        builder.Append("test");

        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(null));
    }

    [Fact]
    public void Build_FluentApi_WorksCorrectly()
    {
        var result = new AITemplateBuilder()
            .WithSeparator(" ")
            .Append("A")
            .Append("B")
            .Append("C")
            .Build();

        Assert.Equal("A B C", result);
    }

    [Fact]
    public void Build_AllEmpty_ReturnsEmptyString()
    {
        var builder = new AITemplateBuilder();
        builder.Append("");
        builder.Append((string)null);
        builder.Append(new AITemplate { Content = "" });

        Assert.Equal(string.Empty, builder.Build());
    }

    private sealed class FakeAITemplateService : IAITemplateService
    {
        private readonly Dictionary<string, string> _templates;

        public FakeAITemplateService(Dictionary<string, string> templates)
        {
            _templates = templates;
        }

        public Task<IReadOnlyList<AITemplate>> ListAsync()
            => Task.FromResult<IReadOnlyList<AITemplate>>([]);

        public Task<AITemplate> GetAsync(string id)
            => Task.FromResult<AITemplate>(null);

        public Task<string> RenderAsync(string id, IDictionary<string, object> arguments = null)
            => Task.FromResult(_templates.TryGetValue(id, out var result) ? result : null);

        public Task<string> MergeAsync(IEnumerable<string> ids, IDictionary<string, object> arguments = null, string separator = "\n\n")
            => Task.FromResult<string>(null);
    }
}
