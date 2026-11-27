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

    // The page gets the code the JavaScript tests exercise, from the assembly, in a script element, without its comments.
    [Fact]
    public void Script_IsTheSourceFileWithoutCommentLinesInAScriptElement()
    {
        // Arrange
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Core", "CrestApps.OrchardCore.Core", "Assets", "js", "invariant-number-inputs.js"));
        var code = source.ReplaceLineEndings("\n").Split('\n').Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal));

        // Act
        var script = InvariantNumberInputsScriptFilter.Script.Value;

        // Assert
        Assert.Equal("<script>" + string.Join('\n', code) + "</script>", script);
        Assert.DoesNotContain("</script", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\n//", script, StringComparison.Ordinal);
        Assert.True(script.Length < source.Length * 3 / 4, $"The inline script is {script.Length} characters from a source of {source.Length}.");
    }

    [Fact]
    public void Compact_LeavesOutBlankAndCommentLinesAndKeepsCode()
    {
        // Arrange
        var source = "// A comment\r\n    const a = 1; // kept: code before it\r\n\r\n        // Indented comment\n    return 'http://x';\n";

        // Act
        var compact = InvariantNumberInputsScriptFilter.Compact(source);

        // Assert
        Assert.Equal("const a = 1; // kept: code before it\nreturn 'http://x';", compact);
    }

    // Every module's forms depend on the host making this call: without it a decimal-comma culture binds 12.50 as 1250
    // again, and nothing else would fail.
    [Fact]
    public void CmsWebHost_CallsAddInvariantNumberInputs()
    {
        // Arrange
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Startup", "CrestApps.OrchardCore.Cms.Web", "Program.cs"));

        // Act
        var code = string.Join('\n', program.ReplaceLineEndings("\n").Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // Assert
        Assert.Contains(".AddInvariantNumberInputs()", code, StringComparison.Ordinal);
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
