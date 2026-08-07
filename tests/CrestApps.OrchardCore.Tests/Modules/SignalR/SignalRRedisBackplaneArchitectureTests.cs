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
            "CrestApps.OrchardCore.SignalR.Redis",
            "Manifest.cs"));
        var startup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR.Redis",
            "Startup.cs"));

        // Act & Assert
        Assert.Contains("SignalRConstants.Feature.Area", manifest, StringComparison.Ordinal);
        Assert.Contains("\"OrchardCore.Redis\"", manifest, StringComparison.Ordinal);
        Assert.Contains("typeof(IRedisService)", startup, StringComparison.Ordinal);
        Assert.Contains("AddStackExchangeRedis()", startup, StringComparison.Ordinal);
        Assert.Contains("SignalRRedisOptionsConfiguration", startup, StringComparison.Ordinal);
    }

    [Fact]
    public void AzureBackplane_IsAnExplicitTenantQualifiedFeature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var manifest = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR.Azure",
            "Manifest.cs"));
        var startup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR.Azure",
            "Startup.cs"));

        // Act & Assert
        Assert.Contains("SignalRConstants.Feature.Area", manifest, StringComparison.Ordinal);
        Assert.Contains("SignalRConstants.Feature.AzureBackplane", startup, StringComparison.Ordinal);
        Assert.Contains("AddAzureSignalR(", startup, StringComparison.Ordinal);
    }

    [Fact]
    public void BaseSignalRModule_HasNoBackplanePackageDependencies()
    {
        // The Redis and Azure backplanes live in their own modules so the base SignalR module stays
        // free of their dependencies. Guard that separation so it is not accidentally reintroduced.

        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.SignalR",
            "CrestApps.OrchardCore.SignalR.csproj"));

        // Act & Assert
        Assert.DoesNotContain("OrchardCore.Redis", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.AspNetCore.SignalR.StackExchangeRedis", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Azure.SignalR", project, StringComparison.Ordinal);
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
