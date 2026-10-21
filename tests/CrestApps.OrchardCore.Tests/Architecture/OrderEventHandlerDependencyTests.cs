using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// The order service is built with every order event handler, so a handler must not take, in its constructor, a
/// service whose construction needs the order service again.
/// </summary>
/// <remarks>
/// Bug: the referral order handler took the checkout session store, which runs the checkout handlers, whose order
/// handler needs the order service. The container has no cycle check for a factory registration, so the first request
/// that needed the order service with Referrals on never answered. Such services are resolved when an event comes.
/// </remarks>
public sealed class OrderEventHandlerDependencyTests
{
    private static readonly Type[] _servicesThatNeedTheOrderService =
    [
        typeof(IOrderService),
        typeof(ICheckoutSessionStore),
        typeof(ICheckoutEngine),
    ];

    [Fact]
    public void OrderEventHandlers_DoNotInjectServicesThatNeedTheOrderService()
    {
        // Arrange
        var handlers = LoadCrestAppsTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IOrderEventHandler).IsAssignableFrom(type))
            .ToList();

        // Act
        var offenders = handlers
            .SelectMany(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => _servicesThatNeedTheOrderService.Contains(parameter.ParameterType))
                .Select(parameter => $"{type.FullName}({parameter.ParameterType.Name})"))
            .ToList();

        // Assert
        Assert.NotEmpty(handlers);
        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> LoadCrestAppsTypes()
    {
        foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll"))
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = [.. exception.Types.Where(type => type is not null)];
            }

            foreach (var type in types)
            {
                yield return type;
            }
        }
    }
}
