using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Every routed action of a Learning controller says which HTTP method it answers. An action without one answers every
/// method, so a form that posts to the same path (or to a path its parameter also matches) can reach the page instead
/// of the action that saves: a quiz override and a new grade scale were never saved that way.
/// </summary>
public sealed class LearningControllerRouteTests
{
    [Fact]
    public void EveryRoutedLearningAction_DeclaresItsHttpMethod()
    {
        // Arrange
        var assemblies = Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.Learning*.dll")
            .Select(path => Assembly.LoadFrom(path))
            .ToList();

        // Act
        var actions = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<NonActionAttribute>() is null)
                .Where(method => method.GetCustomAttributes().Any(attribute => attribute is AdminAttribute { Template: not null } or IRouteTemplateProvider { Template: not null }))
                .Select(method => (Type: type, Method: method)))
            .ToList();
        var offenders = actions
            .Where(action => !action.Method.GetCustomAttributes().OfType<IActionHttpMethodProvider>().Any())
            .Select(action => $"{action.Type.Name}.{action.Method.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.NotEmpty(actions);
        Assert.Empty(offenders);
    }
}
