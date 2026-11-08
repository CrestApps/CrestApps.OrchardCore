using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Workflows.Activities;

namespace CrestApps.OrchardCore.RecipeSchemaExporter;

/// <summary>
/// Finds the schema definitions and workflow activities of every CrestApps assembly next to the exporter.
/// </summary>
/// <remarks>
/// Each module owns the schemas of its own recipe steps, workflow activities, site settings, parts and fields, often as
/// internal types, so the exporter can't look in the Recipes.Core assembly alone. The exporter references the module
/// bundle, which copies every module assembly to its output folder, and this catalog loads them all.
/// </remarks>
internal static class SchemaTypeCatalog
{
    private const string _assemblyPrefix = "CrestApps.OrchardCore";

    private static readonly Lazy<Type[]> _types = new(LoadTypes);

    /// <summary>
    /// Gets the assemblies the types were read from.
    /// </summary>
    public static IReadOnlyList<Assembly> Assemblies { get; private set; } = [];

    /// <summary>
    /// Returns the concrete classes, public or not, that implement <paramref name="contract"/>.
    /// </summary>
    /// <param name="contract">The interface or base class the classes implement.</param>
    public static Type[] GetImplementations(Type contract)
        => _types.Value
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && contract.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Creates an instance of <paramref name="type"/>, resolving its constructor arguments from
    /// <paramref name="serviceProvider"/> and standing in a mock for any interface the exporter does not provide.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <param name="serviceProvider">The services to resolve arguments from, or <see langword="null"/>.</param>
    public static object Create(Type type, IServiceProvider serviceProvider)
    {
        var constructor = type
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var arguments = constructor.GetParameters()
            .Select(parameter => serviceProvider?.GetService(parameter.ParameterType) ?? CreateStandIn(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(arguments);
    }

    /// <summary>
    /// Returns the workflow activities declared by the CrestApps assemblies, by activity name.
    /// </summary>
    /// <returns>The activity name, the name of the class that declares it, and whether it is an event.</returns>
    public static IEnumerable<(string Name, string TypeName, bool IsEvent)> GetWorkflowActivities()
    {
        foreach (var type in GetImplementations(typeof(IActivity)))
        {
            string name;

            try
            {
                // The name of an activity never depends on its services, so an uninitialized instance can report it.
                name = ((IActivity)RuntimeHelpers.GetUninitializedObject(type)).Name;
            }
            catch (Exception exception) when (exception is NullReferenceException or InvalidOperationException or MemberAccessException)
            {
                Console.WriteLine($"Warning: could not read the name of the workflow activity '{type.FullName}': {exception.Message}");

                continue;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return (name, type.Name, typeof(IEvent).IsAssignableFrom(type));
            }
        }
    }

    private static object CreateStandIn(Type parameterType)
    {
        if (parameterType.IsValueType)
        {
            return Activator.CreateInstance(parameterType);
        }

        if (parameterType.IsInterface || (parameterType.IsAbstract && !parameterType.IsSealed))
        {
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(parameterType));
            mock.DefaultValue = DefaultValue.Mock;

            return mock.Object;
        }

        return null;
    }

    private static Type[] LoadTypes()
    {
        var assemblies = new List<Assembly>();
        var loadedNames = AppDomain.CurrentDomain.GetAssemblies()
            .ToDictionary(assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, $"{_assemblyPrefix}*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(path);

            if (name.Contains(".Views", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                assemblies.Add(loadedNames.TryGetValue(name, out var loaded) ? loaded : Assembly.Load(new AssemblyName(name)));
            }
            catch (Exception exception) when (exception is FileLoadException or BadImageFormatException or FileNotFoundException)
            {
                Console.WriteLine($"Warning: could not load '{name}': {exception.Message}");
            }
        }

        Assemblies = assemblies
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToArray();

        return Assemblies
            .SelectMany(GetLoadableTypes)
            .ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null);
        }
    }
}
