using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Services;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.Environment.Extensions.Features;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The commerce admin lists share one filters shape, which a theme can override for one list.
/// </summary>
public sealed class AdminListFiltersShapeTests
{
    [Fact]
    public async Task TheFiltersShape_CarriesTheFormAndTheFilters_InOrder()
    {
        // Arrange
        var status = new AdminListFilter("Options.Status", [new SelectListItem("Active", "Active", true)], "Filter by status", "Status");
        var source = new AdminListFilter("Options.Source", [], "Filter by source", "Source");

        // Act
        var shape = await new ModelShapeFactory().CreateAdminListFiltersAsync("Transactions", "transactions-filter", status, source);

        // Assert
        var model = Assert.IsAssignableFrom<AdminListFiltersViewModel>(shape);
        Assert.Equal(CommerceConstants.Shapes.AdminListFilters, shape.Metadata.Type);
        Assert.Equal("Transactions", model.ListName);
        Assert.Equal("transactions-filter", model.FormId);
        Assert.Equal(["Options.Status", "Options.Source"], model.Filters.Select(filter => filter.Name));
    }

    [Fact]
    public async Task TheFiltersShape_HasAnAlternateNamedByItsList()
    {
        // Arrange
        var shape = await new ModelShapeFactory().CreateAdminListFiltersAsync("Coupons", "coupons-filter");

        // Act
        await DisplayAsync(shape);

        // Assert
        Assert.Contains("CommerceAdminListFilters__Coupons", shape.Metadata.Alternates);
    }

    private static async Task DisplayAsync(IShape shape)
    {
        var builder = new ShapeTableBuilder(Mock.Of<IFeatureInfo>());
        await new AdminListFiltersShapeTableProvider().DiscoverAsync(builder);

        var descriptor = new ShapeDescriptor { ShapeType = CommerceConstants.Shapes.AdminListFilters };

        foreach (var alteration in builder.BuildAlterations().Where(alteration => alteration.ShapeType == descriptor.ShapeType))
        {
            alteration.Alter(descriptor);
        }

        foreach (var displaying in descriptor.DisplayingAsync)
        {
            await displaying(new ShapeDisplayContext { Shape = shape });
        }
    }
}
