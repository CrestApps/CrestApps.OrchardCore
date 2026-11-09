using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.DisplayManagement.Shapes;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Runs Orchard Core's real <see cref="DisplayManager{TModel}"/> in a test container, so a test sees the editor a
/// controller would render: every <c>IDisplayDriver&lt;TModel&gt;</c> registered in the container adds its shapes to
/// the editor's zones, in placement order. Shapes are built by <see cref="ModelShapeFactory"/>, which creates each
/// <c>Initialize&lt;TModel&gt;</c> shape as Orchard Core does, as a proxy of its view model.
/// </summary>
internal static class EditorDisplayManagement
{
    /// <summary>
    /// Registers the display manager and the few services it needs, beside the drivers a test registers.
    /// </summary>
    /// <param name="services">The test container's services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddEditorDisplayManagement(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped(typeof(IDisplayManager<>), typeof(DisplayManager<>));
        services.AddScoped<IShapeFactory, ModelShapeFactory>();
        services.AddScoped(_ => Mock.Of<ILayoutAccessor>());

        return services;
    }

    /// <summary>
    /// Reads the shapes the drivers placed in a zone of an editor, in the order the page renders them.
    /// </summary>
    /// <param name="editor">The editor shape the display manager built.</param>
    /// <param name="zone">The zone, <c>Content</c> by default.</param>
    /// <returns>The shapes of the zone, or none when no driver placed a shape in it.</returns>
    public static IReadOnlyList<IShape> ZoneShapes(IShape editor, string zone = "Content")
    {
        if (!editor.Properties.TryGetValue(zone, out var value) || value is not Shape shape)
        {
            return [];
        }

        return [.. shape.Items.OfType<IShape>()];
    }
}
