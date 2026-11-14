using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Subscriptions.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// RL-3: every commerce endpoint a guest or another site can call, and every route that moves money or checks a secret,
/// carries a request group of Orchard Core's Rate Limits, so the store can limit it under <b>Tools &gt; Rate Limits</b>
/// and the module's built-in limit applies. The allowlist of known exceptions may only shrink.
/// </summary>
public sealed partial class EndpointRateLimitGroupArchitectureTests
{
    // The modules that take payments, hand over goods or check a code or token.
    private static readonly string[] _commerceModules =
    [
        "CrestApps.OrchardCore.AgenticCommerce",
        "CrestApps.OrchardCore.Bookings",
        "CrestApps.OrchardCore.Carts",
        "CrestApps.OrchardCore.Checkout",
        "CrestApps.OrchardCore.Customers",
        "CrestApps.OrchardCore.Downloads",
        "CrestApps.OrchardCore.Fundraising",
        "CrestApps.OrchardCore.GiftCards",
        "CrestApps.OrchardCore.Orders",
        "CrestApps.OrchardCore.PayLater",
        "CrestApps.OrchardCore.PointOfSale",
        "CrestApps.OrchardCore.Shipping",
        "CrestApps.OrchardCore.Stripe",
        "CrestApps.OrchardCore.Subscriptions",
    ];

    // Minimal-API endpoints a guest can call that carry no group yet, as "Module/File.cs: METHOD template". Fix one
    // and remove it here; the test fails while an entry is no longer an exception.
    private static readonly HashSet<string> _allowedMinimalApiExceptions = new(StringComparer.Ordinal)
    {
    };

    // MVC routes that move money or check a secret, by route name, Controller.Action, or Controller.Action:VERB when only
    // one overload counts (the page that shows a form needs no limit; the post that checks it does).
    private static readonly string[] _moneyMvcRoutes =
    [
        CartConstants.RouteNames.Checkout,
        SubscriptionConstants.RouteName.Signup,
        DownloadsConstants.RouteNames.Download,
        CheckoutConstants.RouteNames.DiscountLink,
        "CheckoutController.Display",
        "CheckoutController.DisplayPost",
        "OrderViewController.Track:POST",
        "OrderViewController.Find:POST",
        "DownloadsController.Download",
    ];

    [Fact]
    public void EveryGuestReachableCommerceEndpoint_HasARateLimitGroup()
    {
        var missing = FindUngroupedGuestEndpoints().ToList();
        var unexpected = missing.Where(endpoint => !_allowedMinimalApiExceptions.Contains(endpoint)).Order(StringComparer.Ordinal).ToList();

        Assert.True(unexpected.Count == 0, BuildMessage(
            "These commerce endpoints can be called by a guest or another site and carry no rate limit group. Tag each with " +
            ".WithRateLimitGroup(...) and register the group's built-in limit in a startup that requires the Rate Limits feature:",
            unexpected));
    }

    [Fact]
    public void TheAllowlist_OnlyShrinks()
    {
        var missing = FindUngroupedGuestEndpoints().ToHashSet(StringComparer.Ordinal);
        var stale = _allowedMinimalApiExceptions.Where(entry => !missing.Contains(entry)).Order(StringComparer.Ordinal).ToList();

        Assert.True(stale.Count == 0, BuildMessage("These allowlisted endpoints now carry a group, or are gone; remove them from the allowlist:", stale));
    }

    [Fact]
    public void EveryMoneyMovingMvcRoute_HasARateLimitGroup()
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach (var (controller, action) in EnumerateActions())
        {
            var verbs = action.GetCustomAttributes<HttpMethodAttribute>().ToList();
            var names = verbs.Select(attribute => attribute.Name).Where(name => name is not null)
                .Append($"{controller.Name}.{action.Name}")
                .Concat(verbs.SelectMany(attribute => attribute.HttpMethods).Select(verb => $"{controller.Name}.{action.Name}:{verb}"))
                .Where(name => _moneyMvcRoutes.Contains(name, StringComparer.Ordinal))
                .ToList();

            if (names.Count == 0)
            {
                continue;
            }

            found.UnionWith(names);

            if (action.GetCustomAttribute<RateLimitGroupAttribute>() is null && controller.GetCustomAttribute<RateLimitGroupAttribute>() is null)
            {
                missing.Add($"{controller.FullName}.{action.Name}");
            }
        }

        Assert.True(missing.Count == 0, BuildMessage("These actions move money or check a secret and carry no [RateLimitGroup]:", missing));

        // A renamed route must not silently drop out of the check.
        var notFound = _moneyMvcRoutes.Where(route => !found.Contains(route)).ToList();
        Assert.True(notFound.Count == 0, BuildMessage("These routes are no longer found; update the list:", notFound));
    }

    [Fact]
    public void TheScan_FindsTheKnownGuestEndpoints()
    {
        // The scan is only as good as its parser: it must see the endpoints this change tagged.
        var endpoints = ScanMinimalApiEndpoints().ToList();

        Assert.Contains(endpoints, endpoint => endpoint.Key.EndsWith("POST \"checkout/{sessionId}/payment/begin\"", StringComparison.Ordinal) && endpoint.HasGroup);
        Assert.Contains(endpoints, endpoint => endpoint.Key.EndsWith("POST \"stripe/webhook\"", StringComparison.Ordinal) && endpoint.HasGroup);
        Assert.Contains(endpoints, endpoint => endpoint.Key.Contains("shipping-rates", StringComparison.Ordinal) && endpoint.HasGroup);
    }

    private static IEnumerable<string> FindUngroupedGuestEndpoints()
        => ScanMinimalApiEndpoints().Where(endpoint => endpoint.IsGuestReachable && !endpoint.HasGroup).Select(endpoint => endpoint.Key);

    private static IEnumerable<ScannedEndpoint> ScanMinimalApiEndpoints()
    {
        var modules = Path.Combine(FindRepositoryRoot(), "src", "Modules");

        foreach (var module in _commerceModules)
        {
            var root = Path.Combine(modules, module);

            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in SourceTreeFiles.Enumerate(root, "*.cs", SourceTreeFiles.IsBuildOutputDirectory))
            {
                var text = File.ReadAllText(file);
                var relative = module + "/" + Path.GetRelativePath(root, file).Replace('\\', '/');

                foreach (Match match in MapCall().Matches(text))
                {
                    var end = text.IndexOf(';', match.Index);
                    var chain = end < 0 ? text[match.Index..] : text[match.Index..end];
                    var method = match.Groups["verb"].Value.ToUpperInvariant();
                    var template = match.Groups["template"].Value.Trim();

                    yield return new ScannedEndpoint(
                        $"{relative}: {method} {template}",
                        chain.Contains(".AllowAnonymous()", StringComparison.Ordinal) ||
                            chain.Contains(".AllowGuests()", StringComparison.Ordinal) ||
                            chain.Contains(".DisableAntiforgery()", StringComparison.Ordinal),
                        chain.Contains("WithRateLimitGroup(", StringComparison.Ordinal) ||
                            chain.Contains("RateLimitGroupAttribute(", StringComparison.Ordinal));
                }
            }
        }
    }

    private static IEnumerable<(Type Controller, MethodInfo Action)> EnumerateActions()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Concat(_commerceModules.Select(TryLoad).Where(assembly => assembly is not null))
            .Where(assembly => _commerceModules.Contains(assembly.GetName().Name, StringComparer.Ordinal))
            .Distinct();

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type)))
            {
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    yield return (type, method);
                }
            }
        }
    }

    private static Assembly TryLoad(string name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string BuildMessage(string header, IEnumerable<string> lines)
    {
        var message = new StringBuilder().AppendLine().AppendLine(header);

        foreach (var line in lines)
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found.");
    }

    [GeneratedRegex(@"\.Map(?<verb>Get|Post|Put|Patch|Delete)\(\s*(?<template>[^,]+?)\s*,", RegexOptions.CultureInvariant)]
    private static partial Regex MapCall();

    private sealed record ScannedEndpoint(string Key, bool IsGuestReachable, bool HasGroup);
}
