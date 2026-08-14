using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Asserts that no MVC controller action in the Contact Center, Telephony, Asterisk, or Dialpad
/// modules silently opts out of antiforgery protection while remaining reachable by an unsafe HTTP verb.
/// </summary>
/// <remarks>
/// The catalog and entitlement POST actions carry no explicit <c>[ValidateAntiForgeryToken]</c>
/// attribute; they rely on Orchard Core's globally registered
/// <see cref="AutoValidateAntiforgeryTokenAttribute"/> filter, which validates the antiforgery
/// token on every unsafe request (POST, PUT, PATCH, DELETE) unless an action or controller opts
/// out with <c>[IgnoreAntiforgeryToken]</c>. That reliance is only sound while none of these
/// modules opt out, so this test asserts the invariant rather than assuming it: the only way a
/// cross-site POST could bypass the global filter is an explicit
/// <see cref="IgnoreAntiforgeryTokenAttribute"/>, and none of these controllers declares one.
/// <para>
/// An action is treated as POST-capable when it either declares an unsafe verb constraint (via any
/// <see cref="IActionHttpMethodProvider"/>, which also covers <c>[AcceptVerbs]</c>) or declares no
/// verb constraint at all — an unconstrained MVC action answers every HTTP method, POST included.
/// Actions restricted exclusively to safe verbs (for example a plain <c>[HttpGet]</c>) cannot
/// receive a POST and are therefore out of scope.
/// </para>
/// <para>
/// Provider webhook receivers (Dialpad and Asterisk) legitimately accept unauthenticated external
/// POSTs, but they are implemented as minimal-API endpoints rather than MVC controllers, so they
/// never surface here. If a future controller genuinely needs to ignore antiforgery, this test
/// forces that decision to be explicit and reviewed instead of silent.
/// </para>
/// </remarks>
public sealed class AdminPostAntiforgeryArchitectureTests
{
    private static readonly Assembly[] _moduleAssemblies =
    [
        typeof(CrestApps.OrchardCore.ContactCenter.Startup).Assembly,
        typeof(CrestApps.OrchardCore.Telephony.Startup).Assembly,
        typeof(CrestApps.OrchardCore.Asterisk.Startup).Assembly,
        typeof(CrestApps.OrchardCore.Dialpad.Startup).Assembly,
    ];

    private static readonly string[] _unsafeMethods =
    [
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
    ];

    [Fact]
    public void NoPostCapableControllerActionOptsOutOfAntiforgery()
    {
        // Arrange
        var unsafeActions = EnumerateUnsafeActions().ToList();

        // Act
        var optOuts = unsafeActions
            .Where(action => OptsOutOfAntiforgery(action.Method) || OptsOutOfAntiforgery(action.Controller))
            .Select(action => $"{action.Controller.FullName}.{action.Method.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            optOuts.Count == 0,
            "The following POST-capable actions opt out of antiforgery protection and would bypass Orchard Core's " +
            $"global AutoValidateAntiforgeryToken filter:{Environment.NewLine}{string.Join(Environment.NewLine, optOuts)}");
    }

    [Fact]
    public void TheKnownCatalogPostActionsAreDiscovered()
    {
        // Arrange
        var discoveredControllers = EnumerateUnsafeActions()
            .Select(action => action.Controller.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Contains("SkillsController", discoveredControllers);
        Assert.Contains("QueuesController", discoveredControllers);
        Assert.Contains("AgentEntitlementsController", discoveredControllers);
    }

    private static IEnumerable<(Type Controller, MethodInfo Method)> EnumerateUnsafeActions()
    {
        foreach (var assembly in _moduleAssemblies)
        {
            foreach (var controller in assembly.GetTypes().Where(IsConcreteController))
            {
                var methods = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);

                foreach (var method in methods)
                {
                    if (IsActionMethod(method) && CanReceiveUnsafeRequest(method))
                    {
                        yield return (controller, method);
                    }
                }
            }
        }
    }

    private static bool IsConcreteController(Type type)
    {
        if (type.IsAbstract || !type.IsClass)
        {
            return false;
        }

        return typeof(ControllerBase).IsAssignableFrom(type);
    }

    private static bool IsActionMethod(MethodInfo method)
    {
        if (method.IsStatic || method.IsAbstract || method.IsSpecialName || method.IsGenericMethodDefinition)
        {
            return false;
        }

        var declaringType = method.DeclaringType;

        if (declaringType == typeof(object) || declaringType == typeof(ControllerBase) || declaringType == typeof(Controller))
        {
            return false;
        }

        return method.GetCustomAttribute<NonActionAttribute>(inherit: true) is null;
    }

    private static bool CanReceiveUnsafeRequest(MethodInfo method)
    {
        var httpMethodProviders = method
            .GetCustomAttributes(inherit: true)
            .OfType<IActionHttpMethodProvider>()
            .ToArray();

        if (httpMethodProviders.Length == 0)
        {
            // An action with no verb constraint is reachable by every HTTP method, including POST.
            return true;
        }

        foreach (var provider in httpMethodProviders)
        {
            foreach (var httpMethod in provider.HttpMethods)
            {
                if (_unsafeMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool OptsOutOfAntiforgery(MemberInfo member)
        => member.GetCustomAttributes(inherit: true).OfType<IgnoreAntiforgeryTokenAttribute>().Any();
}
