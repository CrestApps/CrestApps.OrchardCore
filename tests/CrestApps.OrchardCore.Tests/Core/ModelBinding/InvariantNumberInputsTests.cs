using CrestApps.OrchardCore.Core.ModelBinding;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Tests.Core.ModelBinding;

/// <summary>
/// <c>AddInvariantNumberInputs()</c> turns on ASP.NET Core's culture handling for <c>asp-for</c> inputs and writes, on every
/// page, the script that names the other number inputs of a form in an <c>__Invariant</c> field as it is sent. The script
/// itself is tested in <c>tests/js/core/invariant-number-inputs.test.js</c>.
/// </summary>
public sealed class InvariantNumberInputsTests
{
    [Fact]
    public void Setup_WritesAspForInputsInvariantly()
    {
        // Arrange
        var options = new MvcViewOptions();

        // Act
        new InvariantNumberInputsSetup().Configure(options);

        // Assert
        Assert.Equal(FormInputRenderMode.DetectCultureFromInputType, options.HtmlHelperOptions.FormInputRenderMode);
    }

    [Fact]
    public void Setup_AddsTheScriptFilter()
    {
        // Arrange
        var options = new MvcOptions();

        // Act
        new InvariantNumberInputsSetup().Configure(options);

        // Assert
        var filter = Assert.IsType<TypeFilterAttribute>(Assert.Single(options.Filters));

        Assert.Equal(typeof(InvariantNumberInputsScriptFilter), filter.ImplementationType);
    }

    [Fact]
    public async Task ScriptFilter_WritesTheScriptAtTheFootOfAPage()
    {
        // Arrange
        var resourceManager = new Mock<IResourceManager>();
        var filter = new InvariantNumberInputsScriptFilter(resourceManager.Object);
        var nextCalled = false;

        // Act
        await filter.OnResultExecutionAsync(CreateContext(new ViewResult()), () =>
        {
            nextCalled = true;

            return Task.FromResult<ResultExecutedContext>(null);
        });

        // Assert
        Assert.True(nextCalled);
        resourceManager.Verify(manager => manager.RegisterFootScript(It.Is<IHtmlContent>(script => script == InvariantNumberInputsScriptFilter.Script)), Times.Once);
    }

    [Fact]
    public async Task ScriptFilter_LeavesAResponseThatIsNotAPageAlone()
    {
        // Arrange
        var resourceManager = new Mock<IResourceManager>();
        var filter = new InvariantNumberInputsScriptFilter(resourceManager.Object);

        // Act
        await filter.OnResultExecutionAsync(CreateContext(new JsonResult(new { })), () => Task.FromResult<ResultExecutedContext>(null));

        // Assert
        resourceManager.Verify(manager => manager.RegisterFootScript(It.IsAny<IHtmlContent>()), Times.Never);
    }

    // The page gets the script the JavaScript tests exercise, from the assembly, whole and in a script element.
    [Fact]
    public void Script_IsTheSourceFileInAScriptElement()
    {
        // Arrange
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Core", "CrestApps.OrchardCore.Core", "Assets", "js", "invariant-number-inputs.js"));

        // Act
        var script = InvariantNumberInputsScriptFilter.Script.Value;

        // Assert
        Assert.Equal("<script>" + source.ReplaceLineEndings() + "</script>", script.ReplaceLineEndings());
        Assert.DoesNotContain("</script", source, StringComparison.OrdinalIgnoreCase);
    }

    private static ResultExecutingContext CreateContext(IActionResult result)
        => new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [], result, controller: null);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Core", "CrestApps.OrchardCore.Core")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
