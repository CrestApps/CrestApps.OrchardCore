namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class ContactResolutionUiArchitectureTests
{
    [Fact]
    public void ActivityCompletion_RequiresExplicitAmbiguousContactSelectionBeforeDisposition()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var driver = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Drivers/OmnichannelActivityDisplayDriver.cs"));
        var view = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Views/OmnichannelActivityComplete.Edit.cshtml"));
        var controller = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Controllers/ActivitiesController.cs"));
        var workspaceEndpoints = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Endpoints/AgentWorkspaceEndpoints.cs"));

        // Act & Assert
        Assert.Contains(
            "activity.ContactResolutionStatus == ContactResolutionStatus.Ambiguous",
            driver,
            StringComparison.Ordinal);
        Assert.Contains(
            "activity.TryResolveContact(",
            driver,
            StringComparison.Ordinal);
        Assert.Contains(
            "model.SelectedContactContentItemId = activity.ContactContentItemId;",
            driver,
            StringComparison.Ordinal);
        Assert.Contains(
            "asp-for=\"SelectedContactContentItemId\"",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "var activityEditor = await _activityDisplayManager.UpdateEditorAsync",
            controller,
            StringComparison.Ordinal);
        Assert.True(
            controller.IndexOf(
                "var activityEditor = await _activityDisplayManager.UpdateEditorAsync",
                StringComparison.Ordinal) <
            controller.IndexOf(
                "var contact = await _contentManager.GetAsync(activity.ContactContentItemId",
                controller.IndexOf(
                    "public async Task<IActionResult> CompleteAsync",
                    StringComparison.Ordinal),
                StringComparison.Ordinal));

        var workspaceCompletionStart = workspaceEndpoints.IndexOf(
            "private static async Task<IResult> HandleCompleteAsync",
            StringComparison.Ordinal);
        var workspaceActivityLoad = workspaceEndpoints.IndexOf(
            "var activity = await activityManager.FindByIdAsync",
            workspaceCompletionStart,
            StringComparison.Ordinal);
        var workspaceCompletionAuthorization = workspaceEndpoints.IndexOf(
            "OmnichannelConstants.Permissions.CompleteActivity",
            workspaceCompletionStart,
            StringComparison.Ordinal);
        var workspaceDisposition = workspaceEndpoints.IndexOf(
            "dispositionService.ApplyAsync",
            workspaceCompletionStart,
            StringComparison.Ordinal);

        Assert.True(workspaceActivityLoad < workspaceCompletionAuthorization);
        Assert.True(workspaceCompletionAuthorization < workspaceDisposition);
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
