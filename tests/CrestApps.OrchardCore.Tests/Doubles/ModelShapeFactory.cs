using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Implementation;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds a shape the way the real factory does, from the model's own factory (the proxy that
/// <c>CreateAsync&lt;TModel&gt;</c> initializes), and names it by its shape type, so a controller test can read the
/// model a page's shape was built from.
/// </summary>
public sealed class ModelShapeFactory : IShapeFactory
{
    public dynamic New => throw new NotSupportedException();

    public async ValueTask<IShape> CreateAsync(
        string shapeType,
        Func<ValueTask<IShape>> shapeFactory,
        Action<ShapeCreatingContext> creating,
        Action<ShapeCreatedContext> created)
    {
        var shape = await shapeFactory();

        shape.Metadata.Type = shapeType;

        return shape;
    }
}
