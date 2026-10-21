using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The editor of the settings one rate provider keeps on a shipping method. It's shown only for a method of that
/// provider, and stores what the editor posts as the provider's settings on the method.
/// </summary>
/// <typeparam name="TSettings">The provider's settings, stored on the method.</typeparam>
/// <typeparam name="TViewModel">The editor's view model.</typeparam>
internal abstract class ShippingMethodSettingsDisplayDriver<TSettings, TViewModel> : DisplayDriver<ShippingMethod>
    where TSettings : class, new()
    where TViewModel : class, new()
{
    private readonly string _providerName;
    private readonly string _shapeType;

    protected ShippingMethodSettingsDisplayDriver(
        string providerName,
        string shapeType)
    {
        _providerName = providerName;
        _shapeType = shapeType;
    }

    public sealed override IDisplayResult Edit(ShippingMethod method, BuildEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        return Initialize<TViewModel>(_shapeType, model => BuildViewModelAsync(model, method.GetOrCreate<TSettings>()))
            .Location("Content:5");
    }

    public sealed override async Task<IDisplayResult> UpdateAsync(ShippingMethod method, UpdateEditorContext context)
    {
        if (!IsFor(method))
        {
            return null;
        }

        var model = new TViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        method.Put(BuildSettings(model, context));

        return Edit(method, context);
    }

    /// <summary>
    /// Fills the editor from the settings stored on the method.
    /// </summary>
    /// <param name="model">The editor's view model.</param>
    /// <param name="settings">The provider's settings stored on the method.</param>
    protected abstract ValueTask BuildViewModelAsync(TViewModel model, TSettings settings);

    /// <summary>
    /// Validates what the editor posted, reporting each problem on the editor, and returns the settings to store.
    /// </summary>
    /// <param name="model">The posted view model.</param>
    /// <param name="context">The editor's update context.</param>
    protected abstract TSettings BuildSettings(TViewModel model, UpdateEditorContext context);

    private bool IsFor(ShippingMethod method)
        => string.Equals(method.ProviderName, _providerName, StringComparison.OrdinalIgnoreCase);
}
