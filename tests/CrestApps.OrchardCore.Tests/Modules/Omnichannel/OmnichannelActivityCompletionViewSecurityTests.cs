namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class OmnichannelActivityCompletionViewSecurityTests
{
    [Fact]
    public void CompletionView_WorkflowPreview_DoesNotUseInnerHtml()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var viewPath = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.Omnichannel.Managements",
            "Views",
            "OmnichannelActivityComplete.Edit.cshtml");

        // Act
        var source = File.ReadAllText(viewPath);

        // Assert
        Assert.DoesNotContain(".innerHTML", source, StringComparison.Ordinal);
        Assert.Contains("previewTitle.textContent = action.previewTitle;", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
