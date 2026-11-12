namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// SEC-A20 o: the MCP and A2A sample clients call any endpoint with any key their page is given and have no sign-in, so
/// they refuse to start outside Development.
/// </summary>
public sealed class SampleHostDevelopmentGuardTests
{
    [Theory]
    [InlineData("CrestApps.OrchardCore.Samples.McpClient")]
    [InlineData("CrestApps.OrchardCore.Samples.A2AClient")]
    public void SampleClients_RunOnlyInDevelopment(string project)
    {
        var program = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Startup", project, "Program.cs"));

        Assert.Contains("if (!builder.Environment.IsDevelopment())", program);
        Assert.True(
            program.IndexOf("IsDevelopment()", StringComparison.Ordinal) < program.IndexOf("builder.Build()", StringComparison.Ordinal),
            "The guard must run before the app is built.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
