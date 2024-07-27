using System.Reflection;

namespace CrestApps.Support;

public class TypeHelpers
{
    public static IEnumerable<Type> GetClassTypes(Assembly[] assemblies)
    {
        var types = new List<Type>();

        foreach (var assembly in assemblies)
        {
            try
            {
                types.AddRange(assembly.GetTypes().Where(x => x.IsClass && !x.IsInterface));
            }
            catch (Exception)
            {
            }
        }

        return types;
    }

}
