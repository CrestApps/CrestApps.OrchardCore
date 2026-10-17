using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Addresses.Core.Editing;

public static class AddressShapeFactoryExtensions
{
    /// <summary>
    /// The address editor for <paramref name="model"/>, whose fields post under <paramref name="prefix"/>. A display
    /// driver can return <c>Initialize&lt;AddressEditorViewModel&gt;("AddressEditor", …)</c> instead.
    /// </summary>
    public static async ValueTask<IShape> CreateAddressEditorAsync(this IShapeFactory shapeFactory, AddressEditorViewModel model, string prefix)
    {
        ArgumentNullException.ThrowIfNull(model);

        var shape = await shapeFactory.CreateAsync<AddressEditorViewModel>(AddressShapes.Editor, model.CopyTo);

        shape.Metadata.Prefix = prefix;

        return shape;
    }

    /// <summary>
    /// The address printed the way its country's post expects.
    /// </summary>
    public static ValueTask<IShape> CreateAddressDisplayAsync(this IShapeFactory shapeFactory, Address address, bool includeCountry = true)
        => shapeFactory.CreateAsync<AddressDisplayViewModel>(AddressShapes.Display, display =>
        {
            display.Address = address;
            display.IncludeCountry = includeCountry;
        });
}
