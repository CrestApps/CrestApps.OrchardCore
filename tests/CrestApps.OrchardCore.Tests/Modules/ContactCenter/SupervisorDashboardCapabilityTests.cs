namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class SupervisorDashboardCapabilityTests
{
    [Fact]
    public void SupervisorDashboardScript_RendersOnlyServerApprovedMonitoringModes()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "wwwroot",
            "scripts",
            "supervisor-dashboard.js"));

        // Act
        // The bundle carries the shared action helper (Assets/js/shared/supervisor-actions.js, tested with vitest), which
        // offers a mode only when the server listed it for the call, and the board draws each agent's actions through it.
        var readsAvailableModes = script.Contains(
            "if (!has(agent.availableMonitoringModes, mode))",
            StringComparison.Ordinal);
        var rendersThroughTheHelper = script.Contains(
            "interventions.actionsHtml(agent, state)",
            StringComparison.Ordinal);

        // Assert
        Assert.True(readsAvailableModes);
        Assert.True(rendersThroughTheHelper);
        Assert.DoesNotContain(
            "agent.activeInteractionId\n                    ? '<span class=\"cc-agent__actions\">'",
            script,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root could not be found.");
    }
}
