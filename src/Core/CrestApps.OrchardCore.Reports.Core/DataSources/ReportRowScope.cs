using System.Reflection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Records, on the context of a read, that a data set returned only the rows its reader may see. A stored result read
/// with such a context belongs to the reader it was read for: a scheduled report view keeps it for its owner and runs
/// live for everyone else. A custom <see cref="IDataSource"/> that filters rows by <see cref="DataSourceContext.User"/>
/// must call <see cref="MarkRestricted(DataSourceContext)"/>; <c>IndexedReportDataSet</c> and the Contact Center data sets do it for every data set that
/// overrides its <c>RestrictAsync</c>.
/// </summary>
public static class ReportRowScope
{
    private const string PropertyName = "CrestApps.Reports.RowsRestrictedByReader";

    /// <summary>
    /// Records that the rows read with the context depend on its reader.
    /// </summary>
    /// <param name="context">The context of the read.</param>
    public static void MarkRestricted(DataSourceContext context)
    {
        if (context is not null)
        {
            context.Properties[PropertyName] = true;
        }
    }

    /// <summary>
    /// Gets whether any data set read with the context returned only the rows its reader may see.
    /// </summary>
    /// <param name="context">The context of the read.</param>
    public static bool IsRestricted(DataSourceContext context)
        => context is not null && context.Properties.TryGetValue(PropertyName, out var value) && value is true;

    /// <summary>
    /// Gets whether a data set type overrides the protected instance method it inherits under the given name, which a
    /// data set does when it narrows its rows to those its reader may see.
    /// </summary>
    /// <param name="dataSetType">The data set's type.</param>
    /// <param name="methodName">The name of the inherited method, such as <c>RestrictAsync</c>.</param>
    /// <remarks>
    /// <see cref="MethodInfo.GetBaseDefinition"/> returns a <see cref="MethodInfo"/> reflected on the declaring type, so
    /// it is never the same instance as the one read from a subclass, overriding or not. The declaring types are
    /// compared instead.
    /// </remarks>
    public static bool Overrides(Type dataSetType, string methodName)
    {
        ArgumentNullException.ThrowIfNull(dataSetType);

        var method = dataSetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

        return method is not null && method.DeclaringType != method.GetBaseDefinition().DeclaringType;
    }
}
