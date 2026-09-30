using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Descriptors;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Orchard Core's <c>Pager_Links</c> renders a <c>Pager_PageSizeSelector</c> shape when page size selection is on,
/// but only TheTheme and TheAdmin ship a template for it, so a pager on any other theme threw "The shape type
/// 'Pager_PageSizeSelector' is not found". These tests pin the fallback template in the Resources module.
/// </summary>
public sealed class PagerPageSizeSelectorShapeTests
{
    private const string ShapeType = "Pager_PageSizeSelector";

    [Fact]
    public async Task ResourcesFeature_ProvidesThePageSizeSelectorShape_ForAThemeWithoutOne()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "resources-only",
            ProviderProfile = "none",
            Features = ["CrestApps.OrchardCore.Resources"],
        });

        // Act
        // A null theme builds the table from modules alone, which is what a theme without its own template sees.
        var binding = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var shapeTable = await services.GetRequiredService<IShapeTableManager>().GetShapeTableAsync(null);

            return shapeTable.Bindings.TryGetValue(ShapeType, out var shapeBinding) ? shapeBinding : null;
        });

        // Assert
        Assert.NotNull(binding);
        Assert.Contains("CrestApps.OrchardCore.Resources", binding.BindingSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutResourcesFeature_NoModuleProvidesThePageSizeSelectorShape()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "blank",
            ProviderProfile = "none",
            Features = ["OrchardCore.Navigation"],
        });

        // Act
        var hasBinding = await host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var shapeTable = await services.GetRequiredService<IShapeTableManager>().GetShapeTableAsync(null);

            return shapeTable.Bindings.ContainsKey(ShapeType);
        });

        // Assert
        // Guards the premise: if Orchard Core starts shipping its own module-level template, the fallback can go.
        Assert.False(hasBinding);
    }
}
