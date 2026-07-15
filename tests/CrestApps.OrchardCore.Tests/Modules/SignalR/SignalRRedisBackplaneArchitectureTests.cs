namespace CrestApps.OrchardCore.Tests.Modules.SignalR;

public sealed class SignalRRedisBackplaneArchitectureTests
{
    [Fact]
    public void RedisBackplane_IsAnExplicitTenantQualifiedFeature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var manifest = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR",
            "Manifest.cs"));
        var startup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR",
            "RedisBackplaneStartup.cs"));

        // Act & Assert
        Assert.Contains("SignalRConstants.Feature.RedisBackplane", manifest, StringComparison.Ordinal);
        Assert.Contains("\"OrchardCore.Redis\"", manifest, StringComparison.Ordinal);
        Assert.Contains("typeof(IRedisService)", startup, StringComparison.Ordinal);
        Assert.Contains("AddStackExchangeRedis()", startup, StringComparison.Ordinal);
        Assert.Contains("SignalRRedisOptionsConfiguration", startup, StringComparison.Ordinal);
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
