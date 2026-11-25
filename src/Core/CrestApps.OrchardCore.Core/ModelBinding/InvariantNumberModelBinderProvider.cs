using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Supplies <see cref="InvariantNumberModelBinder"/> for action parameters and for the properties of CrestApps models
/// that are a <see cref="decimal"/>, <see cref="double"/> or <see cref="float"/>, and for the elements of those that are a
/// collection or a dictionary of one.
/// </summary>
/// <remarks>
/// Only properties declared by a CrestApps assembly are bound this way; Orchard Core's own forms, and those of other
/// modules on the tenant, keep the framework's reading. Action parameters are always bound this way, because metadata
/// does not say which controller declares one; a floating-point action parameter is posted by a number input or by
/// script, both of which write a dot. A parameter or property bound from the body, a header, the query string, the route
/// or the services, or by a binder of its own, is left to the binder the framework picks for it: only a form value is
/// tagged with the request culture. A collection, an array or a dictionary of numbers is bound by the framework's
/// collection binder, with this binder reading each element.
/// </remarks>
internal sealed class InvariantNumberModelBinderProvider : IModelBinderProvider
{
    // The styles the framework's own FloatingPointTypeModelBinderProvider binds with.
    private const NumberStyles FrameworkStyles = NumberStyles.Float | NumberStyles.AllowThousands;

    private const string CrestAppsAssemblyPrefix = "CrestApps.";

    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsInScope(context.Metadata) || !BindsFromForm(context.BindingInfo))
        {
            return null;
        }

        if (IsNumber(context.Metadata.UnderlyingOrModelType))
        {
            return CreateBinder(context.Metadata.UnderlyingOrModelType, context.Services);
        }

        if (GetElementType(context.Metadata) is { } elementType && IsNumber(Nullable.GetUnderlyingType(elementType) ?? elementType))
        {
            return CreateCollectionBinder(context);
        }

        return null;
    }

    internal static bool IsNumber(Type type)
        => type == typeof(decimal) || type == typeof(double) || type == typeof(float);

    internal static IModelBinder CreateBinder(Type modelType, IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        IModelBinder frameworkBinder = modelType == typeof(decimal)
            ? new DecimalModelBinder(FrameworkStyles, loggerFactory)
            : modelType == typeof(double)
                ? new DoubleModelBinder(FrameworkStyles, loggerFactory)
                : new FloatModelBinder(FrameworkStyles, loggerFactory);

        return new InvariantNumberModelBinder(modelType, frameworkBinder);
    }

    // The framework's own collection, array or dictionary binder, asked through a context that hands it this binder for
    // each element.
    private static IModelBinder CreateCollectionBinder(ModelBinderProviderContext context)
    {
        var elementContext = new InvariantNumberElementBinderProviderContext(context);

        foreach (var provider in context.Services.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders)
        {
            if (provider is InvariantNumberModelBinderProvider)
            {
                continue;
            }

            if (provider.GetBinder(elementContext) is { } binder)
            {
                return binder;
            }
        }

        return null;
    }

    private static Type GetElementType(ModelMetadata metadata)
    {
        if (!metadata.IsEnumerableType || metadata.ModelType == typeof(string))
        {
            return null;
        }

        var dictionary = IsDictionary(metadata.ModelType)
            ? metadata.ModelType
            : metadata.ModelType.GetInterfaces().FirstOrDefault(IsDictionary);

        return dictionary?.GetGenericArguments()[1] ?? metadata.ElementType;
    }

    private static bool IsDictionary(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>);

    // A plain parameter or property has no binding source; [FromForm] is Form, and the binders that read a header or a
    // collection's elements ask for ModelBinding.
    private static bool BindsFromForm(BindingInfo bindingInfo)
        => bindingInfo?.BinderType is null &&
            (bindingInfo?.BindingSource is null ||
            bindingInfo.BindingSource == BindingSource.Form ||
            bindingInfo.BindingSource == BindingSource.ModelBinding);

    private static bool IsInScope(ModelMetadata metadata)
        => metadata.MetadataKind switch
        {
            ModelMetadataKind.Parameter => true,
            ModelMetadataKind.Property => metadata.ContainerType?.Assembly.GetName().Name?.StartsWith(CrestAppsAssemblyPrefix, StringComparison.Ordinal) == true,
            _ => false,
        };
}
