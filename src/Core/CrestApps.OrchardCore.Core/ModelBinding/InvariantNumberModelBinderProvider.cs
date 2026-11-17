using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Supplies <see cref="InvariantNumberModelBinder"/> for action parameters and for the properties of CrestApps models
/// that are a <see cref="decimal"/>, <see cref="double"/> or <see cref="float"/>.
/// </summary>
/// <remarks>
/// Only properties declared by a CrestApps assembly are bound this way; Orchard Core's own forms, and those of other
/// modules on the tenant, keep the framework's reading. Action parameters are always bound this way, because metadata
/// does not say which controller declares one; a floating-point action parameter is posted by a number input or by
/// script, both of which write a dot.
/// </remarks>
internal sealed class InvariantNumberModelBinderProvider : IModelBinderProvider
{
    // The styles the framework's own FloatingPointTypeModelBinderProvider binds with.
    private const NumberStyles FrameworkStyles = NumberStyles.Float | NumberStyles.AllowThousands;

    private const string CrestAppsAssemblyPrefix = "CrestApps.";

    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.UnderlyingOrModelType;

        if ((modelType != typeof(decimal) && modelType != typeof(double) && modelType != typeof(float)) ||
            !IsInScope(context.Metadata))
        {
            return null;
        }

        var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();

        IModelBinder frameworkBinder = modelType == typeof(decimal)
            ? new DecimalModelBinder(FrameworkStyles, loggerFactory)
            : modelType == typeof(double)
                ? new DoubleModelBinder(FrameworkStyles, loggerFactory)
                : new FloatModelBinder(FrameworkStyles, loggerFactory);

        return new InvariantNumberModelBinder(modelType, frameworkBinder);
    }

    private static bool IsInScope(ModelMetadata metadata)
        => metadata.MetadataKind switch
        {
            ModelMetadataKind.Parameter => true,
            ModelMetadataKind.Property => metadata.ContainerType?.Assembly.GetName().Name?.StartsWith(CrestAppsAssemblyPrefix, StringComparison.Ordinal) == true,
            _ => false,
        };
}
