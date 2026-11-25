using CrestApps.OrchardCore.AI.Agent.Settings;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// A settings driver built in a module gets its editor shapes from Orchard Core's shape source generator: a sealed
/// type, written at build time, that derives from the view model and adds <see cref="IShape"/>. The settings tools
/// read the view model behind such a shape as they read the one behind a proxy type made at run time.
/// </summary>
public sealed class CompiledShapeCaptureTests
{
    [Fact]
    public async Task Capture_WhenTheEditorShapeIsACompiledShapeType_ReadsItsViewModel()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddMvcCore()
            .AddDataAnnotations()
            .Services
            .BuildServiceProvider();

        var editorShape = new CompiledTestSettingsShape
        {
            Title = "Contoso",
            PageSize = 10,
        };
        editorShape.Metadata.Type = "TestSettings_Edit";
        editorShape.Metadata.Prefix = "ISite.TestSettings";

        var zone = new Shape();
        await zone.AddAsync(editorShape, "1");

        var root = new ZoneHolding(() => ValueTask.FromResult<IShape>(new Shape()));
        root.Properties["Content"] = zone;

        // Act
        var editor = SiteSettingsEditor.Capture(services, root);

        // Assert
        var section = Assert.Single(editor.Sections);

        Assert.Equal("ISite.TestSettings", section.Name);
        Assert.Equal("Contoso", section.Values["Title"].GetValue<string>());
        Assert.Equal(10, section.Values["PageSize"].GetValue<int>());
        Assert.Empty(editor.Unsupported);
    }

    /// <summary>
    /// A shape type as Orchard Core's shape source generator writes it: sealed, derived from the view model, with
    /// <see cref="IShape"/> added.
    /// </summary>
    public sealed class CompiledTestSettingsShape : TestSettingsViewModel, IShape, IPositioned
    {
        private readonly List<IPositioned> _items = [];

        public ShapeMetadata Metadata { get; } = new();

        public string Id { get; set; }

        public string TagName { get; set; }

        public IList<string> Classes { get; } = [];

        public IDictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

        public IDictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public IReadOnlyList<IPositioned> Items => _items;

        public string Position { get; set; }

        public ValueTask<IShape> AddAsync(object item, string position)
        {
            if (item is IPositioned positioned)
            {
                positioned.Position = position;
                _items.Add(positioned);
            }

            return ValueTask.FromResult<IShape>(this);
        }
    }
}
