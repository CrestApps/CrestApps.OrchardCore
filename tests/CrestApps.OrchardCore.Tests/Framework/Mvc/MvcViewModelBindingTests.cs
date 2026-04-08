using System.Reflection;
using CrestApps.Core.Mvc.Web.Areas.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class MvcViewModelBindingTests
{
    [Fact]
    public void ViewModels_ShouldNotUseListSelectListItemProperties()
    {
        var offenders = GetViewModelTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.PropertyType == typeof(List<SelectListItem>))
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Expected MVC view models to use IEnumerable<SelectListItem> instead of List<SelectListItem>. Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void ViewModels_ShouldMarkSelectListEnumerablesWithBindNever()
    {
        var offenders = GetViewModelTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.PropertyType == typeof(IEnumerable<SelectListItem>) &&
                    property.GetCustomAttribute<BindNeverAttribute>() is null)
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToList();

        Assert.True(offenders.Count == 0, $"Expected MVC view model select-list enumerables to use [BindNever]. Offenders: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<Type> GetViewModelTypes()
    {
        return typeof(AIProfileViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("CrestApps.Core.Mvc.Web.Areas", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("ViewModel", StringComparison.Ordinal));
    }
}
