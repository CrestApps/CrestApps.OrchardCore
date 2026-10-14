namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class SupervisorDashboardCapabilityTests
{
    [Fact]
    public void SupervisorDashboardScript_RendersOnlyServerApprovedMonitoringModes()
    {
        // Arrange
        // The sources, not the built bundle: the build reformats the code, so its text is not what was written. That the
        // bundle carries the helper is checked against Assets.json by tests/js/contact-center/supervisor-actions.test.js.
        var repositoryRoot = FindRepositoryRoot();
        var scripts = Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Assets",
            "js");
        var helper = File.ReadAllText(Path.Combine(scripts, "shared", "supervisor-actions.js"));
        var script = File.ReadAllText(Path.Combine(scripts, "supervisor-dashboard.js"));

        // Act
        // The shared action helper (tested with vitest) offers a mode only when the server listed it for the call, and the
        // board draws each agent's actions through it.
        var readsAvailableModes = helper.Contains(
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
