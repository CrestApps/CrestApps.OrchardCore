using CrestApps.OrchardCore.Taxation.ViewModels;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxationEditorViewModelProxyTests
{
    public static TheoryData<Type> DriverBackedViewModels =>
    [
        typeof(TaxCategoryViewModel),
        typeof(TaxJurisdictionViewModel),
        typeof(TaxRuleViewModel),
    ];

    [Theory]
    [MemberData(nameof(DriverBackedViewModels))]
    public void EditorViewModel_ShouldNotBeSealed_SoTheDisplayManagerCanProxyIt(Type viewModelType)
    {
        // The display drivers render these editors with Initialize<TModel>, which builds a
        // Castle DynamicProxy subclass of the view model. A sealed view model makes the proxy
        // creation throw a TypeLoadException that the display pipeline swallows, silently
        // rendering an empty editor form. Keeping the type open guarantees the editor renders.
        Assert.False(
            viewModelType.IsSealed,
            $"{viewModelType.Name} must not be sealed; the display manager needs to create a runtime proxy of it.");
    }
}
