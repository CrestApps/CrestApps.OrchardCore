using CrestApps.AI.Prompting.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.AI.Prompting;

public sealed class FluidAITemplateEngineTests
{
    private readonly FluidAITemplateEngine _renderer;

    public FluidAITemplateEngineTests()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        _renderer = new FluidAITemplateEngine(
            services,
            NullLogger<FluidAITemplateEngine>.Instance);
    }

    [Fact]
    public async Task RenderAsync_NullTemplate_ReturnsEmpty()
    {
        var result = await _renderer.RenderAsync(null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RenderAsync_EmptyTemplate_ReturnsEmpty()
    {
        var result = await _renderer.RenderAsync(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RenderAsync_PlainText_ReturnsUnchanged()
    {
        var result = await _renderer.RenderAsync("You are a helpful assistant.");

        Assert.Equal("You are a helpful assistant.", result);
    }

    [Fact]
    public async Task RenderAsync_WithVariables_SubstitutesValues()
    {
        var template = "Hello, {{ name }}! You work at {{ company }}.";
        var arguments = new Dictionary<string, object>
        {
            ["name"] = "Alice",
            ["company"] = "Contoso",
        };

        var result = await _renderer.RenderAsync(template, arguments);

        Assert.Equal("Hello, Alice! You work at Contoso.", result);
    }

    [Fact]
    public async Task RenderAsync_WithConditionals_ProcessesLogic()
    {
        var template = "{% if show_tools %}Tools: {{ tools }}{% else %}No tools available{% endif %}";
        var arguments = new Dictionary<string, object>
        {
            ["show_tools"] = true,
            ["tools"] = "hammer, saw",
        };

        var result = await _renderer.RenderAsync(template, arguments);

        Assert.Equal("Tools: hammer, saw", result);
    }

    [Fact]
    public async Task RenderAsync_WithFalseConditional_ReturnsElseBranch()
    {
        var template = "{% if show_tools %}Tools available{% else %}No tools{% endif %}";
        var arguments = new Dictionary<string, object>
        {
            ["show_tools"] = false,
        };

        var result = await _renderer.RenderAsync(template, arguments);

        Assert.Equal("No tools", result);
    }

    [Fact]
    public async Task RenderAsync_WithLoop_RendersIteration()
    {
        var template = "Items: {% for item in items %}{{ item }}, {% endfor %}";
        var arguments = new Dictionary<string, object>
        {
            ["items"] = new[] { "a", "b", "c" },
        };

        var result = await _renderer.RenderAsync(template, arguments);

        Assert.Equal("Items: a, b, c, ", result);
    }

    [Fact]
    public async Task RenderAsync_MissingVariable_RendersEmpty()
    {
        var template = "Hello, {{ name }}!";

        var result = await _renderer.RenderAsync(template);

        Assert.Equal("Hello, !", result);
    }

    [Fact]
    public async Task RenderAsync_InvalidLiquid_ReturnsOriginalTemplate()
    {
        var template = "{% if %}broken{% endif %}";

        var result = await _renderer.RenderAsync(template);

        Assert.Equal(template, result);
    }

    [Fact]
    public void TryValidate_ValidTemplate_ReturnsTrue()
    {
        var valid = _renderer.TryValidate("Hello {{ name }}", out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void TryValidate_InvalidTemplate_ReturnsFalse()
    {
        var valid = _renderer.TryValidate("{% if %}broken{% endif %}", out var errors);

        Assert.False(valid);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void TryValidate_NullTemplate_ReturnsTrue()
    {
        var valid = _renderer.TryValidate(null, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void TryValidate_EmptyTemplate_ReturnsTrue()
    {
        var valid = _renderer.TryValidate(string.Empty, out var errors);

        Assert.True(valid);
        Assert.Empty(errors);
    }
}
