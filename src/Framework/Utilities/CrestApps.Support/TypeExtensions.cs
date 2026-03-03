using System.Collections;
using System.ComponentModel;

namespace CrestApps.Support;

public static class TypeExtensions
{
    public static object GetSafeObject(this Type type, string value)
    {
        if (Nullable.GetUnderlyingType(type) != null && string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trueType = Nullable.GetUnderlyingType(type) ?? type;


        if (trueType == typeof(string))
        {
            return value;
        }

        if (trueType.IsEnum)
        {
            return Enum.Parse(trueType, value);
        }

        if (trueType == typeof(bool))
        {
            if (bool.TryParse(value, out var isValid))
            {
                return isValid;
            }

            return false;
        }

        if (string.IsNullOrWhiteSpace(value) && type.IsNumeric())
        {
            value = "0";
        }

        var tc = TypeDescriptor.GetConverter(type);

        return tc.ConvertFromString(value);
    }

    private static readonly HashSet<Type> _integralNumericTypes =
    [
        typeof(sbyte),
        typeof(byte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong)
    ];

    private static readonly HashSet<Type> _fractionalNumericTypes =
    [
        typeof(float),
        typeof(double),
        typeof(decimal)
    ];

    /// <summary>
    /// Finds the BaseType.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<Type> BaseTypes(this Type type)
    {
        var baseType = type;
        while (true)
        {
            baseType = baseType.BaseType;

            if (baseType == null)
            {
                break;
            }

            yield return baseType;
        }
    }

    /// <summary>
    /// Find any base type that matches the gives type.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public static bool AnyBaseType(this Type type, Func<Type, bool> predicate)
    {
        return type.BaseTypes()
                   .Any(predicate);
    }

    public static Type FirstParticularType(this Type type, Type generic)
    {
        return type.BaseTypes()
                   .FirstOrDefault(generic.IsAssignableFrom);
    }

    /// <summary>
    /// Finds a particular generic type.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="generic"></param>
    /// <returns></returns>
    public static bool IsParticularGeneric(this Type type, Type generic)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == generic;
    }

    /// <summary>
    /// Extension method to determine if a type if numeric.
    /// </summary>
    /// <param name="type">
    /// The type.
    /// </param>
    /// <returns>
    /// True if the type is numeric, otherwise false.
    /// </returns>
    public static bool IsNumeric(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return _integralNumericTypes.Contains(t) || _fractionalNumericTypes.Contains(t);
    }

    /// <summary>
    /// Extension method to determine if a type if integral numeric.
    /// </summary>
    /// <param name="type">
    /// The type.
    /// </param>
    /// <returns>
    /// True if the type is integral numeric, otherwise false.
    /// </returns>
    public static bool IsIntegralNumeric(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return _integralNumericTypes.Contains(t);
    }

    /// <summary>
    /// Extension method to determine if a type if fractional numeric.
    /// </summary>
    /// <param name="type">
    /// The type.
    /// </param>
    /// <returns>
    /// True if the type is fractional numeric, otherwise false.
    /// </returns>
    public static bool IsFractionalNumeric(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return _fractionalNumericTypes.Contains(t);
    }

    public static bool IsDateTime(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t == typeof(DateTime);
    }

    public static bool IsTrueEnum(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t.IsEnum;
    }

    public static bool IsBoolean(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t == typeof(bool);
    }

    public static bool IsString(this Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        return t == typeof(string);
    }

    public static bool IsSingleValueType(this Type type)
    {
        return type.IsNumeric()
            || type.IsBoolean()
            || type.IsDateTime()
            || type.IsString()
            || type.IsTrueEnum();
    }

    public static Type ExtractGenericInterface(this Type queryType, Type interfaceType)
    {
        bool matchesInterface(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType;

        return matchesInterface(queryType) ? queryType : queryType.GetInterfaces().FirstOrDefault(matchesInterface);
    }

    public static Type[] GetTypeArgumentsIfMatch(this Type closedType, Type matchingOpenType)
    {
        if (!closedType.IsGenericType)
        {
            return null;
        }

        var openType = closedType.GetGenericTypeDefinition();

        return (matchingOpenType == openType) ? closedType.GetGenericArguments() : null;
    }

    public static bool IsCompatibleObject(this Type type, object value)
    {
        return (value == null && TypeAllowsNullValue(type)) || type.IsInstanceOfType(value);
    }

    public static bool IsNullableValueType(this Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    public static bool TypeAllowsNullValue(this Type type)
    {
        return !type.IsValueType || IsNullableValueType(type);
    }

    public static bool IsTrueGenericType(this Type type)
    {
        if (type.IsString())
        {
            return false;
        }

        return type.IsArray ||
            (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type.GetGenericTypeDefinition()));
    }
}
