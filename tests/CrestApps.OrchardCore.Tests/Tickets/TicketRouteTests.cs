using System.Reflection;
using CrestApps.OrchardCore.Tickets.Controllers;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The settings pages live under <c>Admin/tickets/desks</c>, <c>statuses</c>, <c>priorities</c> and <c>views</c>. In
/// Orchard's admin routing a parameter doesn't lose to a literal segment, so no ticket route may put a parameter right
/// after <c>tickets/</c>: <c>tickets/{id}</c> once answered <c>tickets/priorities</c> with a 404.
/// </summary>
public sealed class TicketRouteTests
{
    [Fact]
    public void NoTicketRoute_PutsAParameterRightAfterTheSettingsPrefix()
    {
        // Act
        var templates = typeof(TicketsController).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(TicketsController).Namespace)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<AdminAttribute>())
            .Select(attribute => attribute.Template)
            .Where(template => !string.IsNullOrEmpty(template))
            .ToList();

        // Assert
        Assert.Contains("tickets/desks", templates);
        Assert.DoesNotContain(templates, template => template.StartsWith("tickets/{", StringComparison.Ordinal));
    }
}
