using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// The lead filters were placed at the bottom of the inventory load editor, below every other card, where nobody
/// choosing a lead type saw them appear. They now sit inside the record filters card, under the record type: the
/// lead driver places them in the editor's record filters zone, and the record filters card shows that zone.
/// </summary>
public sealed class LeadBatchFilterPlacementTests
{
    [Fact]
    public async Task Edit_PlacesTheLeadFiltersInTheRecordFiltersZone()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("lead-filter-placement");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using var session = store.CreateSession();
            var editor = NewEditor();
            var context = NewEditorContext(editor);
            var driver = await CreateDriverAsync(session);

            // Act
            var result = await driver.EditAsync(new OmnichannelActivityBatch(), context);
            await result.ApplyAsync(context);

            // Assert
            var recordFilters = OmnichannelActivityBatchDisplayDriver.FindRecordFilters(editor);

            Assert.NotNull(recordFilters);
            Assert.Contains(recordFilters.Items, item => item is IShape shape && shape.Metadata.Type == "LeadBatchFilter_Edit");
            Assert.Empty(editor.Zones["Content"].Items);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void FindRecordFilters_WhenNoDriverPlacedAShapeThere_ReturnsNothing()
    {
        // Arrange
        // Without the CRM feature nothing is placed in the zone, and the record filters card must not render an empty
        // zone shape in its place.
        var editor = NewEditor();

        // Act
        var recordFilters = OmnichannelActivityBatchDisplayDriver.FindRecordFilters(editor);

        // Assert
        Assert.Null(recordFilters);
    }

    private static ZoneHolding NewEditor()
        => new(() => ValueTask.FromResult<IShape>(new Shape()));

    private static BuildEditorContext NewEditorContext(IShape editor)
        => new(
            editor,
            groupId: string.Empty,
            isNew: true,
            htmlFieldPrefix: string.Empty,
            new ProxyShapeFactory(),
            layout: null,
            new PostedFormUpdateModel(null));

    private static async Task<LeadBatchFilterDisplayDriver> CreateDriverAsync(ISession session)
    {
        var statuses = new Mock<INamedCatalog<LeadStatus>>();
        statuses
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(x => x.ListTypeDefinitionsAsync())
            .ReturnsAsync([]);

        var contentTypeProvider = new OmnichannelContentTypeProvider(Mock.Of<IServiceScopeFactory>());
        await contentTypeProvider.EnsureInitializedAsync(contentDefinitionManager.Object);

        return new LeadBatchFilterDisplayDriver(
            statuses.Object,
            new LeadSourceProvider(session),
            new LeadListProvider(session),
            new LeadRatingProvider(contentDefinitionManager.Object),
            contentTypeProvider,
            new PassThroughStringLocalizer<LeadBatchFilterDisplayDriver>());
    }

    /// <summary>
    /// Builds the shapes a driver asks for the way the real factory does, from the model's own factory, and names
    /// them by their shape type, which is all placement reads.
    /// </summary>
    private sealed class ProxyShapeFactory : IShapeFactory
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
}
