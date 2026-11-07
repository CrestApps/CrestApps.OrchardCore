using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// A visit's page takes panels from other modules: a display driver of <see cref="ServiceVisit"/> that a module
/// registers puts its shape in the Content zone of the shape the page renders.
/// </summary>
public sealed class VisitPanelsTests
{
    [Fact]
    public async Task APanelAnotherModuleRegisters_ReachesTheVisitsPage()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddEditorDisplayManagement()
            .AddDisplayDriver<ServiceVisit, PartsPanelDisplayDriver>()
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var displayManager = scope.ServiceProvider.GetRequiredService<IDisplayManager<ServiceVisit>>();

        // Act
        var page = await displayManager.BuildDisplayAsync(new ServiceVisit { Number = "V-00001" }, new PostedFormUpdateModel(null), "Detail");
        var shape = Assert.Single(EditorDisplayManagement.ZoneShapes(page));

        // Assert
        Assert.Equal(nameof(ServiceVisit), page.Metadata.Type);
        Assert.Equal("VisitParts", shape.Metadata.Type);
        Assert.Equal("Parts for V-00001", Assert.IsAssignableFrom<ExtraSectionViewModel>(shape).Note);
    }

    /// <summary>
    /// A panel another module adds to a visit's page.
    /// </summary>
    private sealed class PartsPanelDisplayDriver : DisplayDriver<ServiceVisit>
    {
        public override IDisplayResult Display(ServiceVisit model, BuildDisplayContext context)
            => Initialize<ExtraSectionViewModel>("VisitParts", panel => panel.Note = $"Parts for {model.Number}").Location("Detail", "Content:5");
    }
}
