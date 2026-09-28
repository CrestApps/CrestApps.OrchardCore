using System.Reflection;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Prevents Contact Center services from taking an injected collaborator as an optional constructor dependency.
/// </summary>
/// <remarks>
/// An optional injected dependency silently changes behaviour depending on which features a tenant enabled,
/// because the container supplies the declared default when nothing is registered. Every occurrence of the
/// pattern found in this module was a fail-open security boundary: the call-control authorization service, the
/// transfer destination resolver, and the dial ownership check were all skipped entirely when their feature was
/// not enabled, which the type system never surfaced and no test covered.
/// <para>
/// Making the parameter mandatory turns that silent behaviour change into a container resolution failure, which
/// the feature-dependency audit in the activation test project then catches at build time. This test pins the
/// invariant so the pattern cannot be reintroduced on a different service, closing the class of defect rather
/// than the individual instances of it.
/// </para>
/// </remarks>
public sealed class ContactCenterOptionalDependencyTests
{
    [Fact]
    public void NoContactCenterService_TakesAnInjectedCollaboratorAsAnOptionalConstructorParameter()
    {
        var violations = new List<string>();

        foreach (var type in GetContactCenterTypes())
        {
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (var parameter in constructor.GetParameters())
                {
                    if (!parameter.HasDefaultValue || !IsInjectedCollaborator(parameter.ParameterType))
                    {
                        continue;
                    }

                    violations.Add(
                        $"{type.FullName}..ctor declares optional parameter " +
                        $"'{parameter.ParameterType.Name} {parameter.Name} = {parameter.DefaultValue ?? "null"}'.");
                }
            }
        }

        Assert.True(violations.Count == 0, Describe(violations));
    }

    /// <summary>
    /// Determines whether a constructor parameter type represents a collaborator supplied by the container.
    /// </summary>
    /// <param name="parameterType">The parameter type to classify.</param>
    /// <returns><see langword="true"/> when the parameter is an injected collaborator.</returns>
    private static bool IsInjectedCollaborator(Type parameterType)
    {
        if (parameterType == typeof(CancellationToken))
        {
            return false;
        }

        if (parameterType.IsInterface)
        {
            return true;
        }

        // A collection of interfaces is the other shape the container supplies, and an optional one is the same
        // hazard: the caller cannot tell an empty registration apart from a feature that is switched off.
        return parameterType.IsGenericType &&
            parameterType.GetGenericArguments().Length == 1 &&
            parameterType.GetGenericArguments()[0].IsInterface &&
            typeof(System.Collections.IEnumerable).IsAssignableFrom(parameterType);
    }

    /// <summary>
    /// Gets the concrete Contact Center types whose constructors are subject to the invariant.
    /// </summary>
    /// <remarks>
    /// Both the Core services assembly and the module assembly are scanned. The module assembly registers
    /// handlers, drivers, and endpoint services through the same container, so an optional collaborator there is
    /// the identical hazard.
    /// </remarks>
    /// <returns>The candidate types.</returns>
    private static IEnumerable<Type> GetContactCenterTypes()
    {
        Assembly[] assemblies =
        [
            typeof(ICallControlAuthorizationService).Assembly,
            typeof(ContactCenterRealTimeEventHandler).Assembly,
        ];

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract || type.IsAbstract && !type.IsSealed);
    }

    /// <summary>
    /// Builds an assertion message that names every violation and explains how to resolve it.
    /// </summary>
    /// <param name="violations">The individual violations.</param>
    /// <returns>The assertion message.</returns>
    private static string Describe(IEnumerable<string> violations)
    {
        var message = new StringBuilder(
            "Contact Center services must not take an injected collaborator as an optional constructor parameter, " +
            "because the dependency is then silently absent whenever its owning feature is disabled.")
            .AppendLine()
            .AppendLine();

        foreach (var violation in violations)
        {
            message.Append("  - ").AppendLine(violation);
        }

        return message
            .AppendLine()
            .Append("Make the parameter mandatory and register the service in a feature whose declared dependency ")
            .Append("closure can satisfy it.")
            .ToString();
    }
}
