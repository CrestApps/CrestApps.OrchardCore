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
        var readsAvailableModes = script.Contains(
            "var availableModes = agent.availableMonitoringModes || [];",
            StringComparison.Ordinal);
        var rendersAvailableModes = script.Contains(
            "availableModes.map(function (mode)",
            StringComparison.Ordinal);

        // Assert
        Assert.True(readsAvailableModes);
        Assert.True(rendersAvailableModes);
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
