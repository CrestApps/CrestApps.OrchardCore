using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// The context <see cref="InvariantNumberModelBinderProvider"/> asks the framework's collection, array and dictionary
/// binder providers through, so each number element of a collection in scope is read by
/// <see cref="InvariantNumberModelBinder"/>.
/// </summary>
/// <remarks>
/// An element's metadata is that of its type, which says nothing about the model holding the collection, so the
/// provider alone can't tell a CrestApps model's elements from any other's. Everything but a number element is created
/// through the original context, as the framework would.
/// </remarks>
/// <param name="inner">The context of the collection.</param>
internal sealed class InvariantNumberElementBinderProviderContext(ModelBinderProviderContext inner) : ModelBinderProviderContext
{
    private readonly ModelBinderProviderContext _inner = inner;

    public override BindingInfo BindingInfo => _inner.BindingInfo;

    public override ModelMetadata Metadata => _inner.Metadata;

    public override IModelMetadataProvider MetadataProvider => _inner.MetadataProvider;

    public override IServiceProvider Services => _inner.Services;

    public override IModelBinder CreateBinder(ModelMetadata metadata)
        => InvariantNumberModelBinderProvider.IsNumber(metadata.UnderlyingOrModelType)
            ? InvariantNumberModelBinderProvider.CreateBinder(metadata.UnderlyingOrModelType, Services)
            : _inner.CreateBinder(metadata);

    public override IModelBinder CreateBinder(ModelMetadata metadata, BindingInfo bindingInfo)
        => _inner.CreateBinder(metadata, bindingInfo);
}
