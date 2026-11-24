using System.Reflection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Every Learning deployment step has a display driver, so a deployment plan shows its title, its summary and its
/// editor; the external tools step had none and showed as an empty row.
/// </summary>
public sealed class LearningDeploymentStepDriverTests
{
    [Fact]
    public void EveryLearningDeploymentStep_HasADisplayDriver()
    {
        // Arrange
        var types = Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.Learning*.dll")
            .Select(path => Assembly.LoadFrom(path))
            .SelectMany(assembly => assembly.GetTypes())
            .ToList();

        // Act
        var steps = types.Where(type => typeof(DeploymentStep).IsAssignableFrom(type) && !type.IsAbstract).ToList();
        var shown = types
            .Select(type => type.BaseType)
            .Where(baseType => baseType is { IsGenericType: true } && baseType.GetGenericTypeDefinition() == typeof(DisplayDriver<,>) && baseType.GetGenericArguments()[0] == typeof(DeploymentStep))
            .Select(baseType => baseType.GetGenericArguments()[1])
            .ToHashSet();
        var missing = steps.Where(step => !shown.Contains(step)).Select(step => step.Name).Order(StringComparer.Ordinal).ToList();

        // Assert
        Assert.NotEmpty(steps);
        Assert.Empty(missing);
    }
}
